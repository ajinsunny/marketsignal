using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Data;

namespace SignalCopilot.Api.Services;

public class PortfolioAnalyzer : IPortfolioAnalyzer
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PortfolioAnalyzer> _logger;

    // Source tiering for confidence scoring
    private readonly Dictionary<string, double> _sourceTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tier 1: Premium sources (1.0)
        { "Reuters", 1.0 },
        { "Bloomberg", 1.0 },
        { "Wall Street Journal", 1.0 },
        { "Financial Times", 1.0 },

        // Tier 2: Major news outlets (0.85)
        { "CNBC", 0.85 },
        { "MarketWatch", 0.85 },
        { "Barron's", 0.85 },
        { "The Economist", 0.85 },

        // Tier 3: General business news (0.7)
        { "Forbes", 0.7 },
        { "Business Insider", 0.7 },
        { "Yahoo Finance", 0.7 },

        // Tier 4: Other sources (0.5)
    };

    // Keyword-based sentiment magnitude rules
    private readonly Dictionary<string, (double magnitude, string signal)> _sentimentKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Very Positive (0.8-1.0)
        { "breakthrough", (0.9, "Major breakthrough") },
        { "surges", (0.85, "Strong surge") },
        { "soars", (0.85, "Strong surge") },
        { "record high", (0.9, "Record performance") },
        { "beat expectations", (0.8, "Exceeded expectations") },
        { "strong growth", (0.8, "Strong growth") },
        { "revolutionary", (0.9, "Revolutionary development") },

        // Positive (0.5-0.8)
        { "growth", (0.6, "Growth trend") },
        { "gains", (0.6, "Positive gains") },
        { "rises", (0.6, "Rising trend") },
        { "increases", (0.6, "Increasing") },
        { "improved", (0.65, "Improvement") },
        { "partnership", (0.7, "Strategic partnership") },
        { "expansion", (0.7, "Business expansion") },
        { "acquisition", (0.65, "Acquisition activity") },

        // Negative (0.5-0.8)
        { "falls", (-0.6, "Falling trend") },
        { "drops", (-0.6, "Dropping") },
        { "declines", (-0.6, "Declining") },
        { "losses", (-0.65, "Losses") },
        { "miss expectations", (-0.8, "Missed expectations") },
        { "downgrades", (-0.7, "Downgrade") },
        { "concerns", (-0.6, "Concerns raised") },

        // Very Negative (0.8-1.0)
        { "plunges", (-0.85, "Sharp decline") },
        { "crashes", (-0.9, "Crash") },
        { "scandal", (-0.9, "Scandal") },
        { "investigation", (-0.8, "Under investigation") },
        { "lawsuit", (-0.75, "Legal issues") },
        { "recall", (-0.8, "Product recall") },
        { "bankruptcy", (-1.0, "Bankruptcy risk") },
    };

    public PortfolioAnalyzer(ApplicationDbContext context, ILogger<PortfolioAnalyzer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PortfolioAnalysisResult> AnalyzePortfolioAsync(string userId)
    {
        try
        {
            // Get user's holdings
            var holdings = await _context.Holdings
                .Where(h => h.UserId == userId)
                .ToListAsync();

            if (!holdings.Any())
            {
                return new PortfolioAnalysisResult
                {
                    AnalyzedAt = DateTime.UtcNow,
                    TotalHoldings = 0,
                    ImpactsAnalyzed = 0
                };
            }

            // Get recent impacts for user (last 7 days)
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var impacts = await _context.Impacts
                .Include(i => i.Article)
                .Include(i => i.Holding)
                .Where(i => i.Holding.UserId == userId && i.ComputedAt >= sevenDaysAgo)
                .OrderByDescending(i => i.ComputedAt)
                .ToListAsync();

            var result = new PortfolioAnalysisResult
            {
                AnalyzedAt = DateTime.UtcNow,
                TotalHoldings = holdings.Count,
                ImpactsAnalyzed = impacts.Count
            };

            // Group impacts by ticker
            var impactsByTicker = impacts.GroupBy(i => i.Article.Ticker);

            foreach (var tickerGroup in impactsByTicker)
            {
                var ticker = tickerGroup.Key;
                var tickerImpacts = tickerGroup.ToList();

                // Calculate average impact score
                var avgImpactScore = (double)tickerImpacts.Average(i => i.ImpactScore);

                // Analyze keywords and sentiment
                var keySignals = new List<string>();
                var totalMagnitude = 0.0;
                var sourceTierScore = 0.0;

                foreach (var impact in tickerImpacts)
                {
                    var article = impact.Article;
                    var text = $"{article.Headline} {article.Summary}".ToLower();

                    // Check for sentiment keywords
                    foreach (var keyword in _sentimentKeywords)
                    {
                        if (text.Contains(keyword.Key.ToLower()))
                        {
                            totalMagnitude += keyword.Value.magnitude;
                            if (!keySignals.Contains(keyword.Value.signal))
                            {
                                keySignals.Add(keyword.Value.signal);
                            }
                        }
                    }

                    // Calculate source tier confidence
                    var publisher = article.Publisher ?? "Unknown";
                    var tierMultiplier = _sourceTiers.TryGetValue(publisher, out var tier) ? tier : 0.5;
                    sourceTierScore += tierMultiplier;
                }

                // Average the scores
                var avgMagnitude = totalMagnitude / tickerImpacts.Count;
                var avgSourceTier = sourceTierScore / tickerImpacts.Count;

                // Determine source tier label
                var sourceTierLabel = avgSourceTier switch
                {
                    >= 0.95 => "Premium",
                    >= 0.8 => "High Quality",
                    >= 0.65 => "Standard",
                    _ => "Mixed"
                };

                // Calculate combined confidence score
                var confidenceScore = Math.Abs(avgImpactScore) * avgSourceTier;

                // Determine recommendation
                var recommendation = DetermineRecommendation(
                    avgImpactScore,
                    avgMagnitude,
                    confidenceScore,
                    ticker,
                    keySignals,
                    sourceTierLabel,
                    tickerImpacts.Count
                );

                result.Recommendations.Add(recommendation);
            }

            // Sort recommendations by confidence score (descending)
            result.Recommendations = result.Recommendations
                .OrderByDescending(r => r.ConfidenceScore)
                .ToList();

            _logger.LogInformation(
                "Portfolio analysis completed for user {UserId}. {Holdings} holdings, {Impacts} impacts, {Recommendations} recommendations",
                userId, holdings.Count, impacts.Count, result.Recommendations.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing portfolio for user {UserId}", userId);
            throw;
        }
    }

    private RebalanceRecommendation DetermineRecommendation(
        double avgImpactScore,
        double avgMagnitude,
        double confidenceScore,
        string ticker,
        List<string> keySignals,
        string sourceTierLabel,
        int newsCount)
    {
        var recommendation = new RebalanceRecommendation
        {
            Ticker = ticker,
            ConfidenceScore = Math.Min(confidenceScore, 1.0),
            KeySignals = keySignals.Take(3).ToList(),
            SourceTier = sourceTierLabel,
            AverageImpactScore = avgImpactScore,
            NewsCount = newsCount
        };

        // Combined score for decision making
        var combinedScore = (avgImpactScore + avgMagnitude) / 2;

        // Determine action based on combined score and confidence
        if (combinedScore >= 0.5 && confidenceScore >= 0.4)
        {
            recommendation.Action = RecommendationType.StrongBuy;
            recommendation.Suggestion = $"Consider increasing position by 15-20%";
            recommendation.Reasoning = $"Strong positive signals with {sourceTierLabel.ToLower()} source confidence. Average impact: {avgImpactScore:F2}";
        }
        else if (combinedScore >= 0.2 && confidenceScore >= 0.3)
        {
            recommendation.Action = RecommendationType.Buy;
            recommendation.Suggestion = $"Consider increasing position by 5-10%";
            recommendation.Reasoning = $"Positive signals with {sourceTierLabel.ToLower()} source confidence. Average impact: {avgImpactScore:F2}";
        }
        else if (combinedScore <= -0.5 && confidenceScore >= 0.4)
        {
            recommendation.Action = RecommendationType.StrongSell;
            recommendation.Suggestion = $"Consider reducing position by 15-20%";
            recommendation.Reasoning = $"Strong negative signals with {sourceTierLabel.ToLower()} source confidence. Average impact: {avgImpactScore:F2}";
        }
        else if (combinedScore <= -0.2 && confidenceScore >= 0.3)
        {
            recommendation.Action = RecommendationType.Sell;
            recommendation.Suggestion = $"Consider reducing position by 5-10%";
            recommendation.Reasoning = $"Negative signals with {sourceTierLabel.ToLower()} source confidence. Average impact: {avgImpactScore:F2}";
        }
        else
        {
            recommendation.Action = RecommendationType.Hold;
            recommendation.Suggestion = $"Maintain current position";
            recommendation.Reasoning = $"Mixed or neutral signals. Average impact: {avgImpactScore:F2}. Monitor for clearer trends.";
        }

        return recommendation;
    }
}
