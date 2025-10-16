using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

public class SentimentAnalyzer : ISentimentAnalyzer
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SentimentAnalyzer> _logger;
    private readonly IConsensusCalculator? _consensusCalculator;

    // Finance-aware keyword dictionaries for sentiment analysis
    private static readonly Dictionary<string, int> PositiveKeywords = new()
    {
        // Strong positive (magnitude 3)
        { "surge", 3 }, { "soar", 3 }, { "breakthrough", 3 }, { "record", 3 },
        { "boom", 3 }, { "stellar", 3 }, { "exceptional", 3 },
        { "beats by", 3 }, { "exceeds by", 3 }, { "raises guidance", 3 },
        { "margin expansion", 3 }, { "regulatory approval", 3 }, { "reaffirms", 3 },

        // Moderate positive (magnitude 2)
        { "growth", 2 }, { "profit", 2 }, { "gain", 2 }, { "rise", 2 },
        { "increase", 2 }, { "improve", 2 }, { "beat", 2 }, { "exceed", 2 },
        { "strong", 2 }, { "positive", 2 }, { "upgrade", 2 },
        { "raises", 2 }, { "reiterate", 2 }, { "accelerate", 2 },
        { "outperform", 2 }, { "expansion", 2 },

        // Mild positive (magnitude 1)
        { "stable", 1 }, { "steady", 1 }, { "optimistic", 1 }, { "potential", 1 },
        { "maintain", 1 }, { "resilient", 1 }
    };

    private static readonly Dictionary<string, int> NegativeKeywords = new()
    {
        // Strong negative (magnitude 3)
        { "crash", 3 }, { "plunge", 3 }, { "collapse", 3 }, { "crisis", 3 },
        { "scandal", 3 }, { "bankruptcy", 3 }, { "fraud", 3 },
        { "misses by", 3 }, { "falls short by", 3 }, { "cuts guidance", 3 },
        { "margin compression", 3 }, { "doj probe", 3 }, { "sec investigation", 3 },

        // Moderate negative (magnitude 2)
        { "loss", 2 }, { "decline", 2 }, { "drop", 2 }, { "fall", 2 },
        { "miss", 2 }, { "weak", 2 }, { "concern", 2 }, { "downgrade", 2 },
        { "layoff", 2 }, { "cut", 2 }, { "reduce", 2 },
        { "lowers", 2 }, { "slows", 2 }, { "pressure", 2 },
        { "underperform", 2 }, { "contraction", 2 },

        // Mild negative (magnitude 1)
        { "struggle", 1 }, { "uncertain", 1 }, { "volatile", 1 }, { "risk", 1 },
        { "challenge", 1 }, { "headwind", 1 }
    };

    // Rumor/speculation indicators that reduce confidence
    private static readonly HashSet<string> RumorIndicators = new()
    {
        "reportedly", "sources say", "sources familiar", "allegedly",
        "rumor", "speculation", "could", "may", "might", "possibly",
        "poised to", "expected to", "potential", "considering",
        "unconfirmed", "unverified"
    };

    // Promotional/hype phrases that dilute sentiment
    private static readonly HashSet<string> PromotionalPhrases = new()
    {
        "game-changer", "revolutionary", "unprecedented opportunity",
        "must-buy", "can't miss", "poised for explosive growth",
        "set to soar", "next big thing"
    };

    // Source credibility tiers (affects confidence score)
    private static readonly Dictionary<string, decimal> SourceCredibility = new()
    {
        // Tier 1: Premium financial sources (confidence 0.9-1.0)
        { "Bloomberg", 0.95m },
        { "Reuters", 0.95m },
        { "The Wall Street Journal", 0.95m },
        { "Financial Times", 0.95m },
        { "CNBC", 0.90m },

        // Tier 2: Major news outlets (confidence 0.7-0.85)
        { "The New York Times", 0.85m },
        { "CNN Business", 0.80m },
        { "BBC News", 0.85m },
        { "MarketWatch", 0.80m },
        { "Barron's", 0.85m },

        // Tier 3: Tech/industry sources (confidence 0.6-0.75)
        { "TechCrunch", 0.70m },
        { "The Verge", 0.65m },
        { "Ars Technica", 0.70m },
        { "Yahoo Finance", 0.75m },

        // Default for unknown sources
        { "default", 0.50m }
    };

    public SentimentAnalyzer(
        ApplicationDbContext context,
        ILogger<SentimentAnalyzer> logger,
        IConsensusCalculator? consensusCalculator = null)
    {
        _context = context;
        _logger = logger;
        _consensusCalculator = consensusCalculator;
    }

    public async Task<Signal> AnalyzeArticleAsync(Article article)
    {
        var text = $"{article.Headline} {article.Summary}".ToLower();

        // Use EventCategory-based magnitude if available (from taxonomy)
        int baseMagnitude = article.EventCategory.GetDefaultMagnitude();
        EventCategory eventCategory = article.EventCategory;

        // If no event category was set, try to classify from headline
        if (article.EventCategory == EventCategory.Unknown)
        {
            eventCategory = ClassifyEventFromText(text);
            baseMagnitude = eventCategory.GetDefaultMagnitude();
        }

        // Analyze sentiment (positive/negative/neutral)
        var (sentiment, keywordMagnitude, reasoning, hasRumorIndicators) = AnalyzeSentimentAndMagnitude(text);

        // Use EventCategory magnitude as primary, keyword magnitude as refinement
        // If keywords suggest higher magnitude than event category, take the higher value
        int finalMagnitude = Math.Max(baseMagnitude, keywordMagnitude);

        // Calculate base confidence based on source tier and publisher
        var baseConfidence = CalculateConfidence(article.Publisher, article.SourceTier, hasRumorIndicators);

        // Create preliminary signal for consensus calculation
        var preliminarySignal = new Signal
        {
            ArticleId = article.Id,
            Sentiment = sentiment,
            Magnitude = finalMagnitude,
            Confidence = baseConfidence,
            EventCategory = eventCategory,
            Reasoning = $"[{eventCategory.GetDescription()}] {reasoning}",
            AnalyzedAt = DateTime.UtcNow
        };

        _context.Signals.Add(preliminarySignal);
        await _context.SaveChangesAsync();

        // Calculate consensus and apply bonus
        if (_consensusCalculator != null)
        {
            var consensus = await _consensusCalculator.CalculateConsensusAsync(article);

            // Update signal with consensus data
            preliminarySignal.SourceCount = consensus.UniqueSourceCount;
            preliminarySignal.StanceAgreement = consensus.StanceAgreement;
            preliminarySignal.ConsensusFactor = consensus.ConsensusFactor;

            // Apply consensus confidence bonus (cap at 1.0)
            preliminarySignal.Confidence = Math.Min(baseConfidence + consensus.ConfidenceBonus, 1.0m);

            await _context.SaveChangesAsync();
        }

        var signal = preliminarySignal;

        _logger.LogInformation(
            "Analyzed article {ArticleId}: Category={EventCategory}, Sentiment={Sentiment}, Magnitude={Magnitude}, Confidence={Confidence}, Sources={SourceCount}",
            article.Id, eventCategory.GetDescription(), sentiment, finalMagnitude, signal.Confidence, signal.SourceCount);

        return signal;
    }

    public async Task AnalyzeArticlesAsync(List<Article> articles)
    {
        foreach (var article in articles)
        {
            // Check if signal already exists
            var existingSignal = await _context.Signals
                .FirstOrDefaultAsync(s => s.ArticleId == article.Id);

            if (existingSignal != null)
            {
                continue; // Skip already analyzed articles
            }

            await AnalyzeArticleAsync(article);
        }
    }

    private (int sentiment, int magnitude, string reasoning, bool hasRumorIndicators) AnalyzeSentimentAndMagnitude(string text)
    {
        int positiveScore = 0;
        int positiveCount = 0;
        int positiveMagnitude = 0;
        var positiveMatches = new List<string>();

        int negativeScore = 0;
        int negativeCount = 0;
        int negativeMagnitude = 0;
        var negativeMatches = new List<string>();

        // Check for rumor/speculation indicators
        bool hasRumorIndicators = RumorIndicators.Any(indicator => text.Contains(indicator));

        // Check for promotional/hype phrases
        bool hasPromotionalPhrases = PromotionalPhrases.Any(phrase => text.Contains(phrase));

        // **PHASE 2 ENHANCEMENT: Parse quantitative cues for magnitude adjustment**
        var quantitativeBoost = ParseQuantitativeCues(text);

        // Check for positive keywords
        foreach (var (keyword, mag) in PositiveKeywords)
        {
            if (text.Contains(keyword))
            {
                positiveScore += mag;
                positiveCount++;
                positiveMagnitude = Math.Max(positiveMagnitude, mag);
                positiveMatches.Add(keyword);
            }
        }

        // Check for negative keywords
        foreach (var (keyword, mag) in NegativeKeywords)
        {
            if (text.Contains(keyword))
            {
                negativeScore += mag;
                negativeCount++;
                negativeMagnitude = Math.Max(negativeMagnitude, mag);
                negativeMatches.Add(keyword);
            }
        }

        // Dilute positive sentiment if promotional phrases detected
        if (hasPromotionalPhrases && positiveScore > 0)
        {
            positiveScore = (int)Math.Ceiling(positiveScore * 0.7m); // Reduce by 30%
            positiveMagnitude = Math.Max(1, positiveMagnitude - 1); // Lower magnitude
        }

        // Determine overall sentiment
        int sentiment;
        int magnitude;
        string reasoning;

        if (positiveScore == 0 && negativeScore == 0)
        {
            // Neutral
            sentiment = 0;
            magnitude = Math.Max(1, quantitativeBoost); // Apply quantitative boost even to neutral
            reasoning = "No strong sentiment indicators detected. Classified as neutral.";
            if (quantitativeBoost > 0)
            {
                reasoning += $" (Magnitude boosted to {magnitude} based on quantitative metrics)";
            }
        }
        else if (positiveScore > negativeScore)
        {
            // Positive
            sentiment = 1;
            magnitude = Math.Min(3, Math.Max(positiveMagnitude, quantitativeBoost)); // Take higher of keyword or quantitative
            reasoning = $"Positive sentiment detected. Keywords: {string.Join(", ", positiveMatches.Take(3))}";

            if (quantitativeBoost > positiveMagnitude)
            {
                reasoning += $" (Magnitude boosted to {magnitude} based on quantitative metrics)";
            }

            if (hasPromotionalPhrases)
            {
                reasoning += " (Promotional language detected - sentiment adjusted)";
            }
        }
        else if (negativeScore > positiveScore)
        {
            // Negative
            sentiment = -1;
            magnitude = Math.Min(3, Math.Max(negativeMagnitude, quantitativeBoost)); // Take higher of keyword or quantitative
            reasoning = $"Negative sentiment detected. Keywords: {string.Join(", ", negativeMatches.Take(3))}";

            if (quantitativeBoost > negativeMagnitude)
            {
                reasoning += $" (Magnitude boosted to {magnitude} based on quantitative metrics)";
            }
        }
        else
        {
            // Mixed but equal
            sentiment = 0;
            magnitude = Math.Min(3, Math.Max(Math.Max(positiveMagnitude, negativeMagnitude), quantitativeBoost));
            reasoning = $"Mixed sentiment. Positive keywords: {string.Join(", ", positiveMatches.Take(2))}. Negative keywords: {string.Join(", ", negativeMatches.Take(2))}.";
            if (quantitativeBoost > 0)
            {
                reasoning += $" (Magnitude adjusted to {magnitude} based on quantitative metrics)";
            }
        }

        if (hasRumorIndicators)
        {
            reasoning += " [UNVERIFIED/RUMOR]";
        }

        return (sentiment, magnitude, reasoning, hasRumorIndicators);
    }

    /// <summary>
    /// Parse quantitative cues from text to boost magnitude
    /// Examples: "beats by 15%", "$2B acquisition", "raises guidance from $X to $Y"
    /// </summary>
    private int ParseQuantitativeCues(string text)
    {
        int boost = 0;

        // Percentage patterns (e.g., "beats by 15%", "misses by 8%", "up 20%", "down 12%")
        var percentageMatches = System.Text.RegularExpressions.Regex.Matches(
            text,
            @"(beat|miss|exceed|fall|up|down|gain|loss|surge|plunge|rise|drop)[s]?\s+(by\s+)?(\d+(?:\.\d+)?)\s*%",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        foreach (System.Text.RegularExpressions.Match match in percentageMatches)
        {
            if (decimal.TryParse(match.Groups[3].Value, out var percentage))
            {
                // Magnitude boost based on percentage size
                if (percentage >= 15) boost = Math.Max(boost, 3);      // 15%+ → major
                else if (percentage >= 8) boost = Math.Max(boost, 2);  // 8-15% → moderate
                else if (percentage >= 3) boost = Math.Max(boost, 1);  // 3-8% → minor
            }
        }

        // Dollar amount patterns (e.g., "$2B acquisition", "$500M contract", "$10B market cap loss")
        var dollarMatches = System.Text.RegularExpressions.Regex.Matches(
            text,
            @"\$(\d+(?:\.\d+)?)\s*(billion|b|million|m|trillion|t)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        foreach (System.Text.RegularExpressions.Match match in dollarMatches)
        {
            if (decimal.TryParse(match.Groups[1].Value, out var amount))
            {
                var unit = match.Groups[2].Value.ToLower();

                // Convert to billions for comparison
                var billions = unit.StartsWith("t") ? amount * 1000 :
                              unit.StartsWith("b") ? amount :
                              unit.StartsWith("m") ? amount / 1000 : 0;

                // Magnitude boost based on dollar amount
                if (billions >= 5) boost = Math.Max(boost, 3);         // $5B+ → major
                else if (billions >= 1) boost = Math.Max(boost, 2);    // $1-5B → moderate
                else if (billions >= 0.1m) boost = Math.Max(boost, 1); // $100M+ → minor
            }
        }

        // Guidance revision patterns (e.g., "raises guidance from $50 to $60")
        if (text.Contains("guidance", StringComparison.OrdinalIgnoreCase) &&
            (text.Contains("raise", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("cut", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("lower", StringComparison.OrdinalIgnoreCase)))
        {
            boost = Math.Max(boost, 3); // Guidance changes are always major
        }

        // Layoff percentages (e.g., "layoffs 15% of workforce", "cutting 20% jobs")
        if ((text.Contains("layoff", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("job cut", StringComparison.OrdinalIgnoreCase)) &&
            System.Text.RegularExpressions.Regex.IsMatch(text, @"(\d+)\s*%"))
        {
            boost = Math.Max(boost, 2); // Layoffs are at least moderate
        }

        return boost;
    }

    private decimal CalculateConfidence(string? publisher, SourceTier sourceTier, bool hasRumorIndicators = false)
    {
        // **PHASE 2 ENHANCEMENT: Pin Official sources at 1.0 confidence**
        // Base confidence from source tier
        decimal tierConfidence = sourceTier switch
        {
            SourceTier.Official => 1.00m,    // SEC filings, company press releases (upgraded to 1.0)
            SourceTier.Premium => 0.90m,     // Bloomberg, Reuters, WSJ, FT
            SourceTier.Standard => 0.70m,    // CNBC, MarketWatch, etc.
            SourceTier.Social => 0.40m,      // Social media, unverified
            _ => 0.50m                        // Unknown
        };

        // Refine with publisher credibility if available
        if (!string.IsNullOrEmpty(publisher))
        {
            // Try exact match first
            if (SourceCredibility.TryGetValue(publisher, out var publisherConfidence))
            {
                tierConfidence = Math.Max(tierConfidence, publisherConfidence);
            }
            else
            {
                // Try partial match
                foreach (var (source, cred) in SourceCredibility)
                {
                    if (publisher.Contains(source, StringComparison.OrdinalIgnoreCase))
                    {
                        tierConfidence = Math.Max(tierConfidence, cred);
                        break;
                    }
                }
            }
        }

        // Apply rumor penalty: reduce confidence by 30% if rumor indicators detected
        if (hasRumorIndicators)
        {
            tierConfidence *= 0.70m;
        }

        return Math.Round(tierConfidence, 2);
    }

    private EventCategory ClassifyEventFromText(string text)
    {
        // Earnings
        if (text.Contains("earnings") || text.Contains("eps") || text.Contains("revenue"))
        {
            if (text.Contains("beat") || text.Contains("miss") || text.Contains("exceeds") || text.Contains("falls short"))
                return EventCategory.EarningsBeatMiss;
            if (text.Contains("date") || text.Contains("scheduled") || text.Contains("call"))
                return EventCategory.EarningsCalendar;
        }

        // Guidance
        if (text.Contains("guidance") || text.Contains("forecast") || text.Contains("outlook"))
            return EventCategory.GuidanceChange;

        // M&A
        if (text.Contains("merger") || text.Contains("acquisition") || text.Contains("acquires") ||
            text.Contains("partnership") || text.Contains("deal"))
            return EventCategory.MergersAcquisitions;

        // Regulatory/Legal
        if (text.Contains("sec") || text.Contains("investigation") || text.Contains("lawsuit") ||
            text.Contains("regulatory") || text.Contains("antitrust"))
            return EventCategory.RegulatoryLegal;

        // Leadership
        if (text.Contains("ceo") || text.Contains("cfo") || text.Contains("resigns") ||
            text.Contains("appoints") || text.Contains("executive"))
            return EventCategory.LeadershipChange;

        // Layoffs
        if (text.Contains("layoff") || text.Contains("restructuring") || text.Contains("job cuts"))
            return EventCategory.Layoffs;

        // Product Recall
        if (text.Contains("recall") || text.Contains("safety"))
            return EventCategory.ProductRecall;

        // Analyst Rating
        if (text.Contains("upgrade") || text.Contains("downgrade") || text.Contains("analyst") ||
            text.Contains("rating"))
            return EventCategory.AnalystRating;

        // Dividend/Buyback
        if (text.Contains("dividend") || text.Contains("buyback") || text.Contains("share repurchase"))
            return EventCategory.DividendBuyback;

        // Product Launch
        if (text.Contains("launch") || text.Contains("unveils") || text.Contains("announces new"))
            return EventCategory.ProductLaunch;

        // Contract Win
        if (text.Contains("contract") || text.Contains("wins") || text.Contains("awarded"))
            return EventCategory.ContractWin;

        // Macro/Sector
        if (text.Contains("fed") || text.Contains("interest rate") || text.Contains("inflation") ||
            text.Contains("sector") || text.Contains("industry"))
            return EventCategory.MacroSectorShock;

        return EventCategory.Unknown;
    }
}
