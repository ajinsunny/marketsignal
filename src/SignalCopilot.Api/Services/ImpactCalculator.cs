using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

public class ImpactCalculator : IImpactCalculator
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImpactCalculator> _logger;

    public ImpactCalculator(ApplicationDbContext context, ILogger<ImpactCalculator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CalculateImpactsForArticleAsync(Article article)
    {
        // Get the signal for this article
        var signal = await _context.Signals
            .FirstOrDefaultAsync(s => s.ArticleId == article.Id);

        if (signal == null)
        {
            _logger.LogWarning("No signal found for article {ArticleId}. Skipping impact calculation.", article.Id);
            return;
        }

        // Get all users who hold this ticker
        var holdings = await _context.Holdings
            .Include(h => h.User)
            .Where(h => h.Ticker == article.Ticker)
            .ToListAsync();

        if (!holdings.Any())
        {
            _logger.LogInformation("No holdings found for ticker {Ticker}. No impacts to calculate.", article.Ticker);
            return;
        }

        foreach (var holding in holdings)
        {
            // Check if impact already exists
            var existingImpact = await _context.Impacts
                .FirstOrDefaultAsync(i => i.UserId == holding.UserId && i.ArticleId == article.Id);

            if (existingImpact != null)
            {
                continue; // Skip if already calculated
            }

            // Calculate user's total portfolio value for exposure calculation
            var userHoldings = await _context.Holdings
                .Where(h => h.UserId == holding.UserId)
                .ToListAsync();

            decimal totalPortfolioValue = userHoldings
                .Where(h => h.CostBasis.HasValue)
                .Sum(h => h.Shares * h.CostBasis!.Value);

            // Calculate exposure (what % of portfolio is this holding)
            decimal exposure = 0;
            if (totalPortfolioValue > 0 && holding.CostBasis.HasValue)
            {
                decimal holdingValue = holding.Shares * holding.CostBasis.Value;
                exposure = holdingValue / totalPortfolioValue;
            }
            else
            {
                // If no cost basis, use share count ratio
                decimal totalShares = userHoldings.Sum(h => h.Shares);
                exposure = totalShares > 0 ? holding.Shares / totalShares : 0;
            }

            // Ensure exposure is between 0 and 1
            exposure = Math.Clamp(exposure, 0, 1);

            // **PHASE 2 ENHANCEMENT: Apply concentration adjustment**
            // Positions >15% of portfolio get 1.2x weight to highlight concentration risk/opportunity
            decimal concentrationMultiplier = 1.0m;
            if (exposure > 0.15m)
            {
                concentrationMultiplier = 1.2m;
                _logger.LogInformation(
                    "Concentrated position detected for user {UserId}, ticker {Ticker}: {Exposure:P1}. Applying 1.2x multiplier.",
                    holding.UserId, holding.Ticker, exposure);
            }

            // Calculate base exposure with concentration adjustment
            decimal adjustedExposure = Math.Min(1.0m, exposure * concentrationMultiplier);

            // Calculate impact score: direction × magnitude × confidence × adjusted exposure
            decimal impactScore = signal.Sentiment * signal.Magnitude * signal.Confidence * adjustedExposure;

            var impact = new Impact
            {
                UserId = holding.UserId,
                ArticleId = article.Id,
                HoldingId = holding.Id,
                ImpactScore = impactScore,
                Exposure = exposure,
                ComputedAt = DateTime.UtcNow
            };

            _context.Impacts.Add(impact);

            _logger.LogInformation(
                "Calculated impact for user {UserId}, article {ArticleId}: Score={ImpactScore}, Exposure={Exposure}",
                holding.UserId, article.Id, impactScore, exposure);
        }

        await _context.SaveChangesAsync();
    }

    public async Task CalculateImpactsForUserAsync(string userId)
    {
        // Get user's holdings
        var holdings = await _context.Holdings
            .Where(h => h.UserId == userId)
            .ToListAsync();

        if (!holdings.Any())
        {
            _logger.LogInformation("No holdings found for user {UserId}", userId);
            return;
        }

        var tickers = holdings.Select(h => h.Ticker).Distinct().ToList();

        // Get all articles for these tickers that have signals
        var articles = await _context.Articles
            .Include(a => a.Signal)
            .Where(a => tickers.Contains(a.Ticker) && a.Signal != null)
            .ToListAsync();

        foreach (var article in articles)
        {
            await CalculateImpactsForArticleAsync(article);
        }
    }

    public async Task CalculateAllImpactsAsync()
    {
        // Get all articles with signals
        var articles = await _context.Articles
            .Include(a => a.Signal)
            .Where(a => a.Signal != null)
            .ToListAsync();

        _logger.LogInformation("Calculating impacts for {Count} articles", articles.Count);

        foreach (var article in articles)
        {
            await CalculateImpactsForArticleAsync(article);
        }

        _logger.LogInformation("Impact calculation complete");
    }
}
