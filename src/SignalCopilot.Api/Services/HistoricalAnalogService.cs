using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services;

/// <summary>
/// PHASE 4A: Historical Analogs Service
/// Finds similar past events for a ticker + event category combination
/// and calculates historical patterns to provide evidence for recommendations.
///
/// Purpose: Break the "same recommendation loop" by showing users concrete
/// historical evidence of how similar events played out in the past.
/// </summary>
public interface IHistoricalAnalogService
{
    Task<AnalogData?> GetAnalogsAsync(string ticker, EventCategory category, DateTime currentEventDate);
}

public class HistoricalAnalogService : IHistoricalAnalogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HistoricalAnalogService> _logger;

    // Lookback period for finding similar events
    private const int LookbackMonths = 12;

    // Minimum number of historical events needed to generate a pattern
    private const int MinimumAnalogCount = 3;

    public HistoricalAnalogService(
        ApplicationDbContext context,
        ILogger<HistoricalAnalogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Finds similar past events for a ticker + event category combination
    /// and calculates historical price movement patterns.
    /// </summary>
    public async Task<AnalogData?> GetAnalogsAsync(
        string ticker,
        EventCategory category,
        DateTime currentEventDate)
    {
        try
        {
            var lookbackDate = currentEventDate.AddMonths(-LookbackMonths);

            // Find similar past events for this ticker + category
            var historicalEvents = await _context.Articles
                .Include(a => a.Signal)
                .Where(a =>
                    a.Ticker == ticker &&
                    a.EventCategory == category &&
                    a.PublishedAt >= lookbackDate &&
                    a.PublishedAt < currentEventDate && // Exclude current event
                    a.Signal != null) // Must have been analyzed
                .OrderByDescending(a => a.PublishedAt)
                .ToListAsync();

            if (historicalEvents.Count < MinimumAnalogCount)
            {
                _logger.LogInformation(
                    "Insufficient historical data for ticker {Ticker}, category {Category}: found {Count} events, need {Min}",
                    ticker, category.ToString(), historicalEvents.Count, MinimumAnalogCount);
                return null;
            }

            // Calculate pattern based on sentiment distribution
            var sentiments = historicalEvents
                .Where(e => e.Signal != null)
                .Select(e => e.Signal!.Sentiment)
                .ToList();

            var positiveCount = sentiments.Count(s => s > 0);
            var negativeCount = sentiments.Count(s => s < 0);
            var neutralCount = sentiments.Count(s => s == 0);

            var totalCount = historicalEvents.Count;
            var dominantSentiment = positiveCount > negativeCount ? "positive" : "negative";
            var dominantPercentage = Math.Max(positiveCount, negativeCount) * 100.0 / totalCount;

            // Calculate average magnitude for similar events
            var avgMagnitude = historicalEvents
                .Where(e => e.Signal != null)
                .Average(e => (double)e.Signal!.Magnitude);

            // Generate pattern description
            var pattern = GeneratePatternDescription(
                category,
                dominantSentiment,
                dominantPercentage,
                avgMagnitude,
                totalCount);

            _logger.LogInformation(
                "Generated analog pattern for {Ticker} {Category}: {Count} events, {Pattern}",
                ticker, category, totalCount, pattern);

            return new AnalogData
            {
                Count = totalCount,
                Pattern = pattern
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to retrieve analogs for ticker {Ticker}, category {Category}",
                ticker, category);
            return null;
        }
    }

    /// <summary>
    /// Generates a human-readable pattern description based on historical data.
    /// </summary>
    private string GeneratePatternDescription(
        EventCategory category,
        string dominantSentiment,
        double dominantPercentage,
        double avgMagnitude,
        int count)
    {
        // Map category to readable event name
        var eventName = category switch
        {
            EventCategory.EarningsBeatMiss => "earnings beats/misses",
            EventCategory.GuidanceChange => "guidance changes",
            EventCategory.ProductLaunch => "product launches",
            EventCategory.LeadershipChange => "leadership changes",
            EventCategory.MergersAcquisitions => "M&A events",
            EventCategory.RegulatoryLegal => "regulatory/legal events",
            EventCategory.ContractWin => "contract wins",
            EventCategory.ProductRecall => "product recalls",
            EventCategory.Layoffs => "layoffs/restructuring",
            EventCategory.MacroSectorShock => "macro/sector events",
            EventCategory.DividendBuyback => "dividend/buyback events",
            EventCategory.AnalystRating => "analyst rating changes",
            EventCategory.EarningsCalendar => "earnings announcements",
            EventCategory.Unknown => "similar events",
            _ => "similar events"
        };

        // Describe magnitude
        var magnitudeDescription = avgMagnitude switch
        {
            >= 2.5 => "major",
            >= 1.8 => "significant",
            >= 1.3 => "moderate",
            _ => "minor"
        };

        // Build pattern description
        if (dominantPercentage >= 75)
        {
            // Strong pattern: 75%+ agreement
            return $"Similar {eventName}: historically {dominantSentiment} ({dominantPercentage:F0}%, {count} occurrences, typically {magnitudeDescription} impact)";
        }
        else if (dominantPercentage >= 60)
        {
            // Moderate pattern: 60-75% agreement
            return $"Similar {eventName}: tend {dominantSentiment} ({dominantPercentage:F0}%, {count} occurrences, {magnitudeDescription} impact)";
        }
        else
        {
            // Weak pattern: <60% agreement
            return $"Similar {eventName}: mixed historical signals ({count} occurrences, {magnitudeDescription} avg impact)";
        }
    }
}
