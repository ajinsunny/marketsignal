using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

public class SentimentAnalyzer : ISentimentAnalyzer
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SentimentAnalyzer> _logger;

    // Keyword dictionaries for sentiment analysis
    private static readonly Dictionary<string, int> PositiveKeywords = new()
    {
        // Strong positive (magnitude 3)
        { "surge", 3 }, { "soar", 3 }, { "breakthrough", 3 }, { "record", 3 },
        { "boom", 3 }, { "stellar", 3 }, { "exceptional", 3 },

        // Moderate positive (magnitude 2)
        { "growth", 2 }, { "profit", 2 }, { "gain", 2 }, { "rise", 2 },
        { "increase", 2 }, { "improve", 2 }, { "beat", 2 }, { "exceed", 2 },
        { "strong", 2 }, { "positive", 2 }, { "upgrade", 2 },

        // Mild positive (magnitude 1)
        { "stable", 1 }, { "steady", 1 }, { "optimistic", 1 }, { "potential", 1 }
    };

    private static readonly Dictionary<string, int> NegativeKeywords = new()
    {
        // Strong negative (magnitude 3)
        { "crash", 3 }, { "plunge", 3 }, { "collapse", 3 }, { "crisis", 3 },
        { "scandal", 3 }, { "bankruptcy", 3 }, { "fraud", 3 },

        // Moderate negative (magnitude 2)
        { "loss", 2 }, { "decline", 2 }, { "drop", 2 }, { "fall", 2 },
        { "miss", 2 }, { "weak", 2 }, { "concern", 2 }, { "downgrade", 2 },
        { "layoff", 2 }, { "cut", 2 }, { "reduce", 2 },

        // Mild negative (magnitude 1)
        { "struggle", 1 }, { "uncertain", 1 }, { "volatile", 1 }, { "risk", 1 }
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

    public SentimentAnalyzer(ApplicationDbContext context, ILogger<SentimentAnalyzer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Signal> AnalyzeArticleAsync(Article article)
    {
        var text = $"{article.Headline} {article.Summary}".ToLower();

        // Analyze sentiment and magnitude
        var (sentiment, magnitude, reasoning) = AnalyzeSentimentAndMagnitude(text);

        // Calculate confidence based on source
        var confidence = CalculateConfidence(article.Publisher);

        var signal = new Signal
        {
            ArticleId = article.Id,
            Sentiment = sentiment,
            Magnitude = magnitude,
            Confidence = confidence,
            Reasoning = reasoning,
            AnalyzedAt = DateTime.UtcNow
        };

        _context.Signals.Add(signal);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Analyzed article {ArticleId}: Sentiment={Sentiment}, Magnitude={Magnitude}, Confidence={Confidence}",
            article.Id, sentiment, magnitude, confidence);

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

    private (int sentiment, int magnitude, string reasoning) AnalyzeSentimentAndMagnitude(string text)
    {
        int positiveScore = 0;
        int positiveCount = 0;
        int positiveMagnitude = 0;
        var positiveMatches = new List<string>();

        int negativeScore = 0;
        int negativeCount = 0;
        int negativeMagnitude = 0;
        var negativeMatches = new List<string>();

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

        // Determine overall sentiment
        int sentiment;
        int magnitude;
        string reasoning;

        if (positiveScore == 0 && negativeScore == 0)
        {
            // Neutral
            sentiment = 0;
            magnitude = 1;
            reasoning = "No strong sentiment indicators detected. Classified as neutral.";
        }
        else if (positiveScore > negativeScore)
        {
            // Positive
            sentiment = 1;
            magnitude = positiveMagnitude;
            reasoning = $"Positive sentiment detected. Keywords: {string.Join(", ", positiveMatches.Take(3))}";
        }
        else if (negativeScore > positiveScore)
        {
            // Negative
            sentiment = -1;
            magnitude = negativeMagnitude;
            reasoning = $"Negative sentiment detected. Keywords: {string.Join(", ", negativeMatches.Take(3))}";
        }
        else
        {
            // Mixed but equal
            sentiment = 0;
            magnitude = Math.Max(positiveMagnitude, negativeMagnitude);
            reasoning = $"Mixed sentiment. Positive keywords: {string.Join(", ", positiveMatches.Take(2))}. Negative keywords: {string.Join(", ", negativeMatches.Take(2))}.";
        }

        return (sentiment, magnitude, reasoning);
    }

    private decimal CalculateConfidence(string? publisher)
    {
        if (string.IsNullOrEmpty(publisher))
        {
            return SourceCredibility["default"];
        }

        // Try exact match first
        if (SourceCredibility.TryGetValue(publisher, out var confidence))
        {
            return confidence;
        }

        // Try partial match
        foreach (var (source, cred) in SourceCredibility)
        {
            if (publisher.Contains(source, StringComparison.OrdinalIgnoreCase))
            {
                return cred;
            }
        }

        return SourceCredibility["default"];
    }
}
