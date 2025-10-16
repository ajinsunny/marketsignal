using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

/// <summary>
/// Portfolio-level analytics: concentration, exposure, tilt, aggregate impact
/// PHASE 4B: Enhanced with intent-specific metrics and user context
/// </summary>
public interface IPortfolioAnalytics
{
    Task<PortfolioMetrics> GetPortfolioMetricsAsync(string userId);
    Task<decimal> CalculateExposureAsync(string userId, string ticker);
    Task<decimal> CalculateConcentrationPenaltyAsync(decimal exposurePct);

    // PHASE 4B: Intent-specific metrics
    Task<IntentMetrics> GetIntentMetricsAsync(string userId, HoldingIntent intent);
    Task<Dictionary<HoldingIntent, IntentMetrics>> GetAllIntentMetricsAsync(string userId);

    // PHASE 4B: Holding performance tracking
    Task<HoldingPerformance> GetHoldingPerformanceAsync(int holdingId);
}

public class PortfolioAnalytics : IPortfolioAnalytics
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PortfolioAnalytics> _logger;

    public PortfolioAnalytics(ApplicationDbContext context, ILogger<PortfolioAnalytics> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PortfolioMetrics> GetPortfolioMetricsAsync(string userId)
    {
        var holdings = await _context.Holdings
            .Where(h => h.UserId == userId)
            .ToListAsync();

        if (!holdings.Any())
        {
            return new PortfolioMetrics
            {
                TotalValue = 0,
                ConcentrationIndex = 0,
                LargestPosition = null,
                TopConcentrations = new List<PositionData>()
            };
        }

        // Calculate total portfolio value (using shares * cost basis as proxy for value)
        var totalValue = holdings.Sum(h => h.Shares * (h.CostBasis ?? 0));

        // If total value is 0, return empty metrics to avoid division by zero
        if (totalValue == 0)
        {
            return new PortfolioMetrics
            {
                TotalValue = 0,
                ConcentrationIndex = 0,
                LargestPosition = null,
                TopConcentrations = new List<PositionData>()
            };
        }

        // Calculate Herfindahl-Hirschman Index (concentration measure)
        // HHI = sum of squared market shares (0 = diversified, 10000 = single holding)
        var concentrationIndex = holdings.Sum(h =>
        {
            var exposure = (h.Shares * (h.CostBasis ?? 0)) / totalValue;
            return exposure * exposure * 10000; // Scale to 0-10000
        });

        // Find largest position
        var largestPosition = holdings.OrderByDescending(h => h.Shares * (h.CostBasis ?? 0)).First();
        var largestExposure = (largestPosition.Shares * (largestPosition.CostBasis ?? 0)) / totalValue;

        // Top 3 concentrations
        var topConcentrations = holdings
            .Select(h => new PositionData
            {
                Ticker = h.Ticker,
                ExposurePct = (decimal)(h.Shares * (h.CostBasis ?? 0)) / totalValue
            })
            .OrderByDescending(x => x.ExposurePct)
            .Take(3)
            .ToList();

        return new PortfolioMetrics
        {
            TotalValue = totalValue,
            ConcentrationIndex = concentrationIndex,
            LargestPosition = new PositionData
            {
                Ticker = largestPosition.Ticker,
                ExposurePct = largestExposure
            },
            TopConcentrations = topConcentrations
        };
    }

    public async Task<decimal> CalculateExposureAsync(string userId, string ticker)
    {
        var holdings = await _context.Holdings
            .Where(h => h.UserId == userId)
            .ToListAsync();

        if (!holdings.Any())
        {
            return 0;
        }

        var totalValue = holdings.Sum(h => h.Shares * (h.CostBasis ?? 0));
        var tickerHolding = holdings.FirstOrDefault(h => h.Ticker == ticker);

        if (tickerHolding == null || totalValue == 0)
        {
            return 0;
        }

        var tickerValue = tickerHolding.Shares * (tickerHolding.CostBasis ?? 0);
        return tickerValue / totalValue; // Returns 0.0 to 1.0
    }

    /// <summary>
    /// Calculate concentration penalty/boost for impact scores
    /// >15% exposure increases salience (non-linear)
    /// </summary>
    public Task<decimal> CalculateConcentrationPenaltyAsync(decimal exposurePct)
    {
        // Non-linear concentration adjustment
        // < 5%: 0.9x (reduce salience)
        // 5-15%: 1.0x (neutral)
        // 15-25%: 1.2x (increase salience)
        // 25-40%: 1.5x (high concentration warning)
        // >40%: 2.0x (extreme concentration)

        decimal multiplier = exposurePct switch
        {
            < 0.05m => 0.90m,
            < 0.15m => 1.00m,
            < 0.25m => 1.20m,
            < 0.40m => 1.50m,
            _ => 2.00m
        };

        return Task.FromResult(multiplier);
    }

    // PHASE 4B: Intent-specific metrics implementation

    /// <summary>
    /// Get metrics for holdings with a specific intent (Trade, Accumulate, Income, Hold)
    /// </summary>
    public async Task<IntentMetrics> GetIntentMetricsAsync(string userId, HoldingIntent intent)
    {
        var holdings = await _context.Holdings
            .Where(h => h.UserId == userId && h.Intent == intent)
            .ToListAsync();

        if (!holdings.Any())
        {
            return new IntentMetrics
            {
                Intent = intent,
                Count = 0,
                TotalValue = 0,
                AverageExposure = 0,
                AverageHoldingPeriodDays = 0
            };
        }

        var allHoldings = await _context.Holdings
            .Where(h => h.UserId == userId)
            .ToListAsync();

        var totalPortfolioValue = allHoldings.Sum(h => h.Shares * (h.CostBasis ?? 0));
        var intentValue = holdings.Sum(h => h.Shares * (h.CostBasis ?? 0));

        var avgExposure = totalPortfolioValue > 0 ? intentValue / totalPortfolioValue : 0;

        var avgHoldingPeriod = holdings
            .Where(h => h.AcquiredAt.HasValue)
            .Select(h => (DateTime.UtcNow - h.AcquiredAt!.Value).Days)
            .DefaultIfEmpty(0)
            .Average();

        return new IntentMetrics
        {
            Intent = intent,
            Count = holdings.Count,
            TotalValue = intentValue,
            AverageExposure = avgExposure,
            AverageHoldingPeriodDays = (int)avgHoldingPeriod
        };
    }

    /// <summary>
    /// Get metrics for all intents at once
    /// </summary>
    public async Task<Dictionary<HoldingIntent, IntentMetrics>> GetAllIntentMetricsAsync(string userId)
    {
        var allIntents = Enum.GetValues<HoldingIntent>();
        var result = new Dictionary<HoldingIntent, IntentMetrics>();

        foreach (var intent in allIntents)
        {
            result[intent] = await GetIntentMetricsAsync(userId, intent);
        }

        return result;
    }

    /// <summary>
    /// Get performance metrics for a specific holding
    /// </summary>
    public async Task<HoldingPerformance> GetHoldingPerformanceAsync(int holdingId)
    {
        var holding = await _context.Holdings
            .FirstOrDefaultAsync(h => h.Id == holdingId);

        if (holding == null)
        {
            throw new ArgumentException($"Holding {holdingId} not found");
        }

        var holdingPeriodDays = holding.AcquiredAt.HasValue
            ? (DateTime.UtcNow - holding.AcquiredAt.Value).Days
            : 0;

        // Calculate total impact over holding period
        var impacts = await _context.Impacts
            .Where(i => i.HoldingId == holdingId)
            .ToListAsync();

        var totalImpact = impacts.Sum(i => i.ImpactScore);
        var positiveImpacts = impacts.Count(i => i.ImpactScore > 0);
        var negativeImpacts = impacts.Count(i => i.ImpactScore < 0);

        return new HoldingPerformance
        {
            HoldingId = holdingId,
            Ticker = holding.Ticker,
            HoldingPeriodDays = holdingPeriodDays,
            TotalImpactScore = totalImpact,
            PositiveImpactsCount = positiveImpacts,
            NegativeImpactsCount = negativeImpacts,
            Intent = holding.Intent
        };
    }
}

/// <summary>
/// Portfolio-level metrics for analytics and display
/// </summary>
public class PortfolioMetrics
{
    public decimal TotalValue { get; set; }

    /// <summary>
    /// Herfindahl-Hirschman Index (0-10000)
    /// 0-1500: Diversified
    /// 1500-2500: Moderate concentration
    /// >2500: High concentration
    /// </summary>
    public decimal ConcentrationIndex { get; set; }

    public PositionData? LargestPosition { get; set; }

    public List<PositionData> TopConcentrations { get; set; } = new();
}

/// <summary>
/// Position data for concentration analysis
/// </summary>
public class PositionData
{
    public string Ticker { get; set; } = string.Empty;
    public decimal ExposurePct { get; set; }
}

/// <summary>
/// PHASE 4B: Intent-specific metrics for portfolio analysis
/// Tracks performance and allocation by investment intent
/// </summary>
public class IntentMetrics
{
    public HoldingIntent Intent { get; set; }
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageExposure { get; set; } // As percentage of portfolio
    public int AverageHoldingPeriodDays { get; set; }
}

/// <summary>
/// PHASE 4B: Individual holding performance metrics
/// Tracks impact history and holding period performance
/// </summary>
public class HoldingPerformance
{
    public int HoldingId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int HoldingPeriodDays { get; set; }
    public decimal TotalImpactScore { get; set; }
    public int PositiveImpactsCount { get; set; }
    public int NegativeImpactsCount { get; set; }
    public HoldingIntent Intent { get; set; }
}
