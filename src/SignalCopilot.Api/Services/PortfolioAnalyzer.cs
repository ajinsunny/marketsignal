using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services;

public class PortfolioAnalyzer : IPortfolioAnalyzer
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PortfolioAnalyzer> _logger;
    private readonly IHistoricalAnalogService _analogService;

    public PortfolioAnalyzer(
        ApplicationDbContext context,
        ILogger<PortfolioAnalyzer> logger,
        IHistoricalAnalogService analogService)
    {
        _context = context;
        _logger = logger;
        _analogService = analogService;
    }

    public async Task<PortfolioAnalysisResult> AnalyzePortfolioAsync(string userId)
    {
        try
        {
            // **PHASE 3: Get user for personalization (RiskProfile and CashBuffer are on ApplicationUser)**
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

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
                    ImpactsAnalyzed = 0,
                    Summary = new PortfolioSummary
                    {
                        OverallAdvice = "No holdings in portfolio. Add stocks to start receiving personalized market insights.",
                        Rationale = "You haven't added any holdings yet. Start by adding tickers to your portfolio.",
                        MarketSentiment = "Neutral",
                        KeyActions = new List<string> { "Add holdings to begin analysis" },
                        RiskAssessment = "No risk - portfolio is empty"
                    }
                };
            }

            // Get recent impacts for user (last 7 days) with recency decay
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var impacts = await _context.Impacts
                .Include(i => i.Article)
                    .ThenInclude(a => a.Signal)
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

            if (!impacts.Any())
            {
                result.Summary = new PortfolioSummary
                {
                    OverallAdvice = "No recent market activity detected for your holdings. Your portfolio appears stable.",
                    Rationale = "No significant news or market events affecting your holdings in the past 7 days.",
                    MarketSentiment = "Neutral",
                    KeyActions = new List<string> { "Continue monitoring", "Consider refreshing news data" },
                    RiskAssessment = "Low risk - minimal volatility detected"
                };
                return result;
            }

            // Group impacts by ticker and analyze using ACTUAL Impact Scores
            var impactsByTicker = impacts.GroupBy(i => i.Article.Ticker);

            foreach (var tickerGroup in impactsByTicker)
            {
                var ticker = tickerGroup.Key;
                var tickerImpacts = tickerGroup.ToList();

                // Apply recency decay to impact scores
                var weightedImpacts = ApplyRecencyDecay(tickerImpacts);

                // Calculate average ACTUAL impact score (personalized to user's exposure)
                var avgImpactScore = weightedImpacts.Average(w => w.score);
                var totalMagnitude = weightedImpacts.Sum(w => Math.Abs(w.score));

                // Get source quality from Signal confidence
                var avgConfidence = tickerImpacts
                    .Where(i => i.Article.Signal != null)
                    .Average(i => (double)(i.Article.Signal?.Confidence ?? 0.5m));

                // Calculate source tier based on publishers
                var publishers = tickerImpacts.Select(i => i.Article.Publisher ?? "Unknown").Distinct().ToList();
                var sourceTierLabel = DetermineSourceTier(publishers);

                // Combined confidence: Impact Score already includes exposure, so we factor in source confidence
                var confidenceScore = Math.Min(avgConfidence * (totalMagnitude / tickerImpacts.Count), 1.0);

                // Get user's exposure for this ticker
                var userExposure = tickerImpacts.First().Exposure;

                // **PHASE 3: Get holding details for personalized context**
                var holding = holdings.First(h => h.Ticker == ticker);

                // **PHASE 4A: Fetch historical analogs for evidence-based recommendations**
                // Get the most common event category from the impacts for this ticker
                var mostCommonCategory = tickerImpacts
                    .Where(i => i.Article.EventCategory != EventCategory.Unknown)
                    .GroupBy(i => i.Article.EventCategory)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault();

                // Get the most recent article date for context
                var mostRecentArticleDate = tickerImpacts
                    .Max(i => i.Article.PublishedAt);

                AnalogData? analogs = null;
                if (mostCommonCategory != EventCategory.Unknown)
                {
                    analogs = await _analogService.GetAnalogsAsync(
                        ticker,
                        mostCommonCategory,
                        mostRecentArticleDate
                    );
                }

                // Generate recommendation based on ACTUAL Impact Scores + User Context
                var recommendation = DetermineRecommendation(
                    avgImpactScore,
                    totalMagnitude,
                    confidenceScore,
                    userExposure,
                    ticker,
                    tickerImpacts,
                    sourceTierLabel,
                    user,
                    holding
                );

                // Attach analogs to recommendation
                recommendation.Analogs = analogs;

                result.Recommendations.Add(recommendation);
            }

            // Sort recommendations by absolute impact score (biggest impact first)
            result.Recommendations = result.Recommendations
                .OrderByDescending(r => Math.Abs(r.AverageImpactScore))
                .ToList();

            // Generate comprehensive portfolio summary
            result.Summary = GeneratePortfolioSummary(result.Recommendations, holdings.Count, impacts.Count);

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

    private List<(Impact impact, double score, double weight)> ApplyRecencyDecay(List<Impact> impacts)
    {
        var now = DateTime.UtcNow;
        var result = new List<(Impact, double, double)>();

        foreach (var impact in impacts)
        {
            var ageInDays = (now - impact.ComputedAt).TotalDays;

            // Exponential decay: newer news has more weight
            // Half-life of 2 days means news loses 50% weight every 2 days
            var decayFactor = Math.Pow(0.5, ageInDays / 2.0);

            // Apply decay to the impact score
            var weightedScore = (double)impact.ImpactScore * decayFactor;

            result.Add((impact, weightedScore, decayFactor));
        }

        return result;
    }

    private string DetermineSourceTier(List<string> publishers)
    {
        var tierScores = new Dictionary<string, double>
        {
            { "Reuters", 1.0 }, { "Bloomberg", 1.0 }, { "Wall Street Journal", 1.0 }, { "Financial Times", 1.0 },
            { "CNBC", 0.85 }, { "MarketWatch", 0.85 }, { "Barron's", 0.85 },
            { "Forbes", 0.7 }, { "Business Insider", 0.7 }, { "Yahoo Finance", 0.7 }
        };

        var avgTier = publishers.Average(p => tierScores.TryGetValue(p, out var score) ? score : 0.5);

        return avgTier switch
        {
            >= 0.95 => "Premium",
            >= 0.8 => "High Quality",
            >= 0.65 => "Standard",
            _ => "Mixed"
        };
    }

    private RebalanceRecommendation DetermineRecommendation(
        double avgImpactScore,
        double totalMagnitude,
        double confidenceScore,
        decimal userExposure,
        string ticker,
        List<Impact> impacts,
        string sourceTierLabel,
        ApplicationUser? user,
        Holding holding)
    {
        var recommendation = new RebalanceRecommendation
        {
            Ticker = ticker,
            ConfidenceScore = confidenceScore,
            KeySignals = ExtractKeySignals(impacts),
            SourceTier = sourceTierLabel,
            AverageImpactScore = avgImpactScore,
            NewsCount = impacts.Count
        };

        // **PHASE 3: Calculate holding context for personalization**
        var exposurePercent = (double)userExposure * 100;
        var isConcentrated = userExposure > 0.15m;
        var holdingPeriodDays = holding.AcquiredAt.HasValue
            ? (DateTime.UtcNow - holding.AcquiredAt.Value).Days
            : 0;

        // Calculate unrealized P/L if cost basis is available
        // Note: We don't have current price in this context, so we'll note that in reasoning
        string unrealizedPL = holding.CostBasis.HasValue
            ? "(P/L requires current price data)"
            : "No cost basis";

        // Get user's risk profile and cash buffer (from ApplicationUser)
        var riskProfile = user?.RiskProfile ?? RiskProfile.Balanced;
        var holdingIntent = holding.Intent;
        var cashBuffer = user?.CashBuffer;

        // **PHASE 3: Adjust thresholds based on risk profile**
        // Conservative: Lower thresholds (act sooner on negative signals)
        // Aggressive: Higher thresholds (tolerate more risk)
        var (strongThreshold, moderateThreshold) = riskProfile switch
        {
            RiskProfile.Conservative => (0.25, 0.12),  // More sensitive
            RiskProfile.Balanced => (0.35, 0.18),      // Standard
            RiskProfile.Aggressive => (0.45, 0.25),     // Less sensitive
            _ => (0.35, 0.18)
        };

        // Further adjust for concentrated positions
        if (isConcentrated)
        {
            strongThreshold *= 0.85;      // Lower threshold for concentrated = more cautious
            moderateThreshold *= 0.85;
        }

        // **PHASE 3: RECOMMENDATION PLAYBOOK MATRIX**
        // Match (Impact × RiskProfile × HoldingIntent) to personalized recommendation

        if (avgImpactScore >= strongThreshold && confidenceScore >= 0.3)
        {
            // STRONG POSITIVE SIGNAL
            recommendation.Action = RecommendationType.StrongBuy;
            recommendation.Suggestion = GenerateSuggestion(RecommendationType.StrongBuy, riskProfile, holdingIntent, cashBuffer, isConcentrated);
            recommendation.Reasoning = GenerateReasoning(
                avgImpactScore, confidenceScore, exposurePercent, isConcentrated, holdingPeriodDays,
                riskProfile, holdingIntent, ticker, "strong positive", cashBuffer
            );
        }
        else if (avgImpactScore >= moderateThreshold && confidenceScore >= 0.25)
        {
            // MODERATE POSITIVE SIGNAL
            recommendation.Action = RecommendationType.Buy;
            recommendation.Suggestion = GenerateSuggestion(RecommendationType.Buy, riskProfile, holdingIntent, cashBuffer, isConcentrated);
            recommendation.Reasoning = GenerateReasoning(
                avgImpactScore, confidenceScore, exposurePercent, isConcentrated, holdingPeriodDays,
                riskProfile, holdingIntent, ticker, "moderate positive", cashBuffer
            );
        }
        else if (avgImpactScore <= -strongThreshold && confidenceScore >= 0.3)
        {
            // STRONG NEGATIVE SIGNAL
            recommendation.Action = RecommendationType.StrongSell;
            recommendation.Suggestion = GenerateSuggestion(RecommendationType.StrongSell, riskProfile, holdingIntent, cashBuffer, isConcentrated);
            recommendation.Reasoning = GenerateReasoning(
                avgImpactScore, confidenceScore, exposurePercent, isConcentrated, holdingPeriodDays,
                riskProfile, holdingIntent, ticker, "strong negative", cashBuffer
            );
        }
        else if (avgImpactScore <= -moderateThreshold && confidenceScore >= 0.25)
        {
            // MODERATE NEGATIVE SIGNAL
            recommendation.Action = RecommendationType.Sell;
            recommendation.Suggestion = GenerateSuggestion(RecommendationType.Sell, riskProfile, holdingIntent, cashBuffer, isConcentrated);
            recommendation.Reasoning = GenerateReasoning(
                avgImpactScore, confidenceScore, exposurePercent, isConcentrated, holdingPeriodDays,
                riskProfile, holdingIntent, ticker, "moderate negative", cashBuffer
            );
        }
        else
        {
            // NEUTRAL/MIXED SIGNAL
            recommendation.Action = RecommendationType.Hold;
            recommendation.Suggestion = GenerateSuggestion(RecommendationType.Hold, riskProfile, holdingIntent, cashBuffer, isConcentrated);
            recommendation.Reasoning = GenerateReasoning(
                avgImpactScore, confidenceScore, exposurePercent, isConcentrated, holdingPeriodDays,
                riskProfile, holdingIntent, ticker, "neutral", cashBuffer
            );
        }

        return recommendation;
    }

    /// <summary>
    /// Generate personalized action suggestion based on Risk Profile × Holding Intent
    /// </summary>
    private string GenerateSuggestion(RecommendationType action, RiskProfile riskProfile, HoldingIntent intent, decimal? cashBuffer, bool isConcentrated)
    {
        return (action, riskProfile, intent) switch
        {
            // STRONG BUY playbooks
            (RecommendationType.StrongBuy, RiskProfile.Aggressive, HoldingIntent.Accumulate) =>
                cashBuffer.HasValue && cashBuffer > 1000
                    ? $"Add aggressively: increase position by 12-18% using available cash (${cashBuffer:N0} buffer)"
                    : "Add aggressively: consider increasing position by 12-18% if cash permits",

            (RecommendationType.StrongBuy, RiskProfile.Conservative, HoldingIntent.Accumulate) =>
                "Build gradually: add 3-5% now, set limit orders for dips to accumulate over next 2-4 weeks",

            (RecommendationType.StrongBuy, _, HoldingIntent.Trade) =>
                "Short-term opportunity: add 5-10% with target exit at +8-12% gain within 5-10 trading days",

            (RecommendationType.StrongBuy, _, HoldingIntent.Income) =>
                "Income boost opportunity: verify dividend safety, then add 5-8% to increase yield",

            // BUY playbooks
            (RecommendationType.Buy, RiskProfile.Aggressive, HoldingIntent.Accumulate) =>
                "Moderate add: increase position by 5-8%, monitor for continued momentum",

            (RecommendationType.Buy, RiskProfile.Conservative, _) =>
                "Cautious add: consider 2-3% increase only if position is <10% of portfolio",

            (RecommendationType.Buy, _, HoldingIntent.Trade) =>
                "Tactical entry: add 3-5% with stop-loss at -5%, target +5-8% gain",

            // STRONG SELL playbooks
            (RecommendationType.StrongSell, RiskProfile.Conservative, _) when isConcentrated =>
                "Risk reduction priority: trim position by 15-25% immediately to limit downside. Your conservative profile requires swift action on concentrated holdings.",

            (RecommendationType.StrongSell, _, HoldingIntent.Trade) =>
                "Exit signal: reduce by 30-50% or exit entirely depending on technical levels and stop-loss discipline",

            (RecommendationType.StrongSell, RiskProfile.Aggressive, _) =>
                "Downside protection: trim 8-12%, reassess thesis. Hold remaining if long-term conviction intact",

            // SELL playbooks
            (RecommendationType.Sell, RiskProfile.Conservative, _) =>
                "Risk mitigation: trim position by 8-12% over next week, monitor closely",

            (RecommendationType.Sell, _, HoldingIntent.Accumulate) =>
                "Pause accumulation: trim 5-8%, wait for clearer trend before resuming buys",

            (RecommendationType.Sell, _, HoldingIntent.Trade) =>
                "Reduce exposure: trim 10-15%, set tighter stop-loss on remainder",

            // HOLD playbooks
            (RecommendationType.Hold, _, HoldingIntent.Income) =>
                "Monitor yield: maintain position, verify dividend security, watch for ex-dividend dates",

            (RecommendationType.Hold, _, HoldingIntent.Trade) =>
                "Watch for direction: set alerts at ±3-5% from current, be ready to scale in/out on breakout",

            (RecommendationType.Hold, RiskProfile.Balanced, _) =>
                "Maintain position: hold current allocation, continue monitoring for trend clarity",

            // Default fallbacks
            (RecommendationType.StrongBuy, _, _) => "Consider significant position increase of 10-15%",
            (RecommendationType.Buy, _, _) => "Consider modest position increase of 5-8%",
            (RecommendationType.StrongSell, _, _) => "Consider significant position reduction of 12-18%",
            (RecommendationType.Sell, _, _) => "Consider modest position reduction of 5-8%",
            _ => "Maintain current position and monitor"
        };
    }

    /// <summary>
    /// Generate personalized reasoning with user-specific context
    /// </summary>
    private string GenerateReasoning(
        double avgImpactScore,
        double confidenceScore,
        double exposurePercent,
        bool isConcentrated,
        int holdingPeriodDays,
        RiskProfile riskProfile,
        HoldingIntent intent,
        string ticker,
        string signalType,
        decimal? cashBuffer)
    {
        var reasoning = new System.Text.StringBuilder();

        // Lead with impact and confidence
        reasoning.Append($"{signalType.ToUpper()} impact detected for {ticker}: {avgImpactScore:F2} score with {confidenceScore:P0} confidence. ");

        // Add user context
        reasoning.Append($"Your {exposurePercent:F1}% position ");
        if (isConcentrated)
        {
            reasoning.Append($"(concentrated >15%) ");
        }

        if (holdingPeriodDays > 0)
        {
            reasoning.Append($"held for {holdingPeriodDays} days ");
        }

        reasoning.Append($"with '{intent}' intent. ");

        // Risk profile framing
        reasoning.Append(riskProfile switch
        {
            RiskProfile.Conservative => "Your conservative profile prioritizes capital preservation—this suggests ",
            RiskProfile.Aggressive => "Your aggressive profile targets growth—this suggests ",
            RiskProfile.Balanced => "Your balanced approach—this suggests ",
            _ => "This suggests "
        });

        // Intent-specific guidance
        reasoning.Append((signalType, intent) switch
        {
            ("strong positive", HoldingIntent.Accumulate) => "an opportunity to build your position methodically. ",
            ("strong positive", HoldingIntent.Trade) => "a tactical entry point with defined risk/reward. ",
            ("strong positive", HoldingIntent.Income) => "reviewing dividend safety before adding exposure. ",
            ("moderate positive", HoldingIntent.Accumulate) => "continuing your accumulation strategy cautiously. ",
            ("strong negative", HoldingIntent.Trade) => "exiting or tightening stop-loss immediately. ",
            ("strong negative", HoldingIntent.Accumulate) => "pausing accumulation until thesis is validated. ",
            ("strong negative", HoldingIntent.Hold) => "reassessing your long-term thesis critically. ",
            ("moderate negative", _) => "reducing exposure to manage downside risk. ",
            _ => "maintaining vigilance and monitoring for trend confirmation. "
        });

        // Cash buffer context
        if (cashBuffer.HasValue && signalType.Contains("positive"))
        {
            if (cashBuffer.Value > 5000)
            {
                reasoning.Append($"With ${cashBuffer.Value:N0} cash buffer, you have liquidity to act. ");
            }
            else if (cashBuffer.Value < 1000)
            {
                reasoning.Append($"Limited cash (${cashBuffer.Value:N0}) suggests waiting or using small increments. ");
            }
        }

        // Concentration warning
        if (isConcentrated && signalType.Contains("negative"))
        {
            reasoning.Append($"⚠️ Concentrated position amplifies risk—consider trimming to diversify. ");
        }

        return reasoning.ToString();
    }

    private List<string> ExtractKeySignals(List<Impact> impacts)
    {
        var signals = new List<string>();

        // Extract key information from the articles
        foreach (var impact in impacts.Take(3))  // Top 3 most impactful
        {
            if (impact.Article.Signal != null)
            {
                var sentiment = impact.Article.Signal.Sentiment switch
                {
                    1 => "Positive",
                    -1 => "Negative",
                    _ => "Neutral"
                };

                var magnitude = impact.Article.Signal.Magnitude switch
                {
                    3 => "High",
                    2 => "Medium",
                    _ => "Low"
                };

                signals.Add($"{sentiment} ({magnitude} magnitude)");
            }
        }

        return signals.Distinct().ToList();
    }

    private PortfolioSummary GeneratePortfolioSummary(
        List<RebalanceRecommendation> recommendations,
        int totalHoldings,
        int totalImpacts)
    {
        var summary = new PortfolioSummary();

        if (recommendations.Count == 0)
        {
            summary.OverallAdvice = "No recent market activity to analyze. Your portfolio appears stable with minimal news impact.";
            summary.Rationale = "We haven't detected any significant market events or news articles affecting your holdings in the past 7 days.";
            summary.MarketSentiment = "Neutral";
            summary.KeyActions = new List<string> { "Continue monitoring your positions", "Consider setting up alerts for high-impact news" };
            summary.RiskAssessment = "Low risk - minimal market volatility detected";
            return summary;
        }

        // Categorize recommendations
        var strongBuys = recommendations.Count(r => r.Action == RecommendationType.StrongBuy);
        var buys = recommendations.Count(r => r.Action == RecommendationType.Buy);
        var holds = recommendations.Count(r => r.Action == RecommendationType.Hold);
        var sells = recommendations.Count(r => r.Action == RecommendationType.Sell);
        var strongSells = recommendations.Count(r => r.Action == RecommendationType.StrongSell);

        // Calculate WEIGHTED average sentiment based on absolute impact scores
        var totalAbsImpact = recommendations.Sum(r => Math.Abs(r.AverageImpactScore));
        var weightedImpact = recommendations.Sum(r => r.AverageImpactScore * Math.Abs(r.AverageImpactScore)) / totalAbsImpact;
        var avgConfidence = recommendations.Average(r => r.ConfidenceScore);

        // Determine market sentiment
        if (weightedImpact > 0.3)
            summary.MarketSentiment = "Strongly Positive";
        else if (weightedImpact > 0.1)
            summary.MarketSentiment = "Positive";
        else if (weightedImpact < -0.3)
            summary.MarketSentiment = "Strongly Negative";
        else if (weightedImpact < -0.1)
            summary.MarketSentiment = "Negative";
        else
            summary.MarketSentiment = "Neutral/Mixed";

        // Generate overall advice
        var bullishCount = strongBuys + buys;
        var bearishCount = strongSells + sells;

        if (bullishCount > bearishCount && bullishCount >= recommendations.Count * 0.4)
        {
            summary.OverallAdvice = $"Your portfolio is experiencing predominantly positive market signals across {bullishCount} holdings. " +
                $"Based on your personalized exposure levels, consider strategically increasing positions in high-conviction opportunities.";
        }
        else if (bearishCount > bullishCount && bearishCount >= recommendations.Count * 0.4)
        {
            summary.OverallAdvice = $"Your portfolio faces notable headwinds with negative signals across {bearishCount} holdings. " +
                $"Given your current exposure levels, consider reducing risk by trimming underperforming positions and reallocating to stronger performers.";
        }
        else
        {
            summary.OverallAdvice = $"Your portfolio shows mixed signals with balanced positive and negative indicators. " +
                $"Based on your exposure profile, maintain current positions while monitoring for clearer directional trends.";
        }

        // Generate detailed rationale
        var rationaleBuilder = new System.Text.StringBuilder();
        rationaleBuilder.AppendLine($"**Analysis Overview:** Analyzed {totalImpacts} market events across {totalHoldings} holdings using personalized impact scores based on your portfolio exposure.");
        rationaleBuilder.AppendLine();

        if (strongBuys > 0)
        {
            var topBuys = recommendations.Where(r => r.Action == RecommendationType.StrongBuy).Take(2);
            rationaleBuilder.AppendLine($"**Strong Buy Signals ({strongBuys}):** {string.Join(", ", topBuys.Select(r => r.Ticker))} show exceptional growth potential. " +
                $"Average personalized impact: +{topBuys.Average(r => r.AverageImpactScore):F2}. These positions warrant increased allocation based on your exposure profile.");
            rationaleBuilder.AppendLine();
        }

        if (buys > 0)
        {
            rationaleBuilder.AppendLine($"**Buy Signals ({buys}):** Additional holdings show moderate positive momentum. " +
                $"Consider modest position increases where your current exposure allows for growth.");
            rationaleBuilder.AppendLine();
        }

        if (holds > 0)
        {
            rationaleBuilder.AppendLine($"**Hold Positions ({holds}):** These holdings exhibit neutral or mixed signals relative to your portfolio. " +
                $"Continue monitoring without immediate action required.");
            rationaleBuilder.AppendLine();
        }

        if (sells > 0 || strongSells > 0)
        {
            var topSells = recommendations.Where(r => r.Action == RecommendationType.Sell || r.Action == RecommendationType.StrongSell).Take(2);
            rationaleBuilder.AppendLine($"**Sell Signals ({sells + strongSells}):** {string.Join(", ", topSells.Select(r => r.Ticker))} are experiencing negative pressure. " +
                $"Average personalized impact: {topSells.Average(r => r.AverageImpactScore):F2}. Consider reducing exposure to mitigate downside risk.");
            rationaleBuilder.AppendLine();
        }

        rationaleBuilder.AppendLine($"**Confidence Level:** {avgConfidence:P0} average confidence. " +
            $"Impact scores are personalized to YOUR portfolio exposure, making them more relevant than generic market sentiment.");

        summary.Rationale = rationaleBuilder.ToString();

        // Generate key actions
        summary.KeyActions = new List<string>();

        if (strongBuys > 0)
        {
            var topBuy = recommendations.First(r => r.Action == RecommendationType.StrongBuy);
            summary.KeyActions.Add($"Priority: Increase {topBuy.Ticker} (Impact: +{topBuy.AverageImpactScore:F2}, Confidence: {topBuy.ConfidenceScore:P0})");
        }

        if (strongSells > 0)
        {
            var topSell = recommendations.First(r => r.Action == RecommendationType.StrongSell);
            summary.KeyActions.Add($"Risk Mitigation: Reduce {topSell.Ticker} (Impact: {topSell.AverageImpactScore:F2}, Confidence: {topSell.ConfidenceScore:P0})");
        }

        if (buys > 0)
        {
            summary.KeyActions.Add($"Growth: Consider modest increases in {buys} positions showing positive momentum");
        }

        if (holds > 0)
        {
            summary.KeyActions.Add($"Monitor: Watch {holds} holdings for trend clarity before adjusting");
        }

        if (summary.KeyActions.Count == 0)
        {
            summary.KeyActions.Add("Continue monitoring - no immediate action required");
        }

        // Risk assessment based on volatility and exposure
        var highImpactCount = recommendations.Count(r => Math.Abs(r.AverageImpactScore) > 0.4);
        var highExposureRisk = recommendations.Any(r => r.AverageImpactScore < -0.3);

        if (highImpactCount >= recommendations.Count * 0.5 || highExposureRisk)
        {
            summary.RiskAssessment = "Elevated risk - significant volatility detected in your portfolio. Impact scores show notable price movement potential. Consider tighter monitoring.";
        }
        else if (avgConfidence >= 0.4)
        {
            summary.RiskAssessment = "Moderate risk with good signal quality. Recommendations backed by reliable sources and clear impact patterns.";
        }
        else
        {
            summary.RiskAssessment = "Low to moderate risk. Mixed signal quality suggests cautious position sizing and continued monitoring.";
        }

        return summary;
    }
}
