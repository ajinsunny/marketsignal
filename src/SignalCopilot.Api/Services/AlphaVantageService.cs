using System.Text.Json;
using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

/// <summary>
/// Alpha Vantage News API integration for premium financial news
/// Free tier: 25 API calls per day
/// Documentation: https://www.alphavantage.co/documentation/#news-sentiment
/// </summary>
public class AlphaVantageService : DataSourceServiceBase
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;

    public override string SourceName => "Alpha Vantage";
    public override SourceType SourceType => SourceType.News;
    public override SourceTier SourceTier => SourceTier.Premium;

    public override bool IsEnabled =>
        !string.IsNullOrEmpty(_configuration["DataSources:AlphaVantage:ApiKey"]) &&
        _configuration["DataSources:AlphaVantage:ApiKey"] != "your_api_key_here" &&
        _configuration.GetValue<bool>("DataSources:AlphaVantage:Enabled", false);

    public AlphaVantageService(
        HttpClient httpClient,
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<AlphaVantageService> logger)
        : base(configuration, logger)
    {
        _httpClient = httpClient;
        _context = context;
    }

    public override async Task<List<Article>> FetchForTickerAsync(string ticker)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("Alpha Vantage is not enabled. Check configuration.");
            return new List<Article>();
        }

        try
        {
            var apiKey = _configuration["DataSources:AlphaVantage:ApiKey"];
            var url = $"https://www.alphavantage.co/query?function=NEWS_SENTIMENT&tickers={ticker}&apikey={apiKey}&limit=50";

            _logger.LogInformation("Fetching news from Alpha Vantage for ticker {Ticker}", ticker);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Alpha Vantage returned {StatusCode}: {ErrorContent}",
                    response.StatusCode, errorContent);
                return new List<Article>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var newsResponse = JsonSerializer.Deserialize<AlphaVantageNewsResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (newsResponse?.Feed == null || !newsResponse.Feed.Any())
            {
                _logger.LogInformation("No articles found from Alpha Vantage for ticker {Ticker}", ticker);
                return new List<Article>();
            }

            var articles = new List<Article>();

            foreach (var feedItem in newsResponse.Feed.Take(15)) // Limit to 15 articles per ticker
            {
                // Check if article already exists by URL
                var existingArticle = await _context.Articles
                    .FirstOrDefaultAsync(a => a.SourceUrl == feedItem.Url);

                if (existingArticle != null)
                {
                    continue; // Skip duplicates
                }

                // Parse published date
                DateTime publishedAt;
                if (!DateTime.TryParseExact(feedItem.TimePublished, "yyyyMMddTHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out publishedAt))
                {
                    publishedAt = DateTime.UtcNow; // Fallback
                }

                var article = new Article
                {
                    Ticker = ticker.ToUpper(),
                    Headline = feedItem.Title?.Trim() ?? "No title",
                    Summary = feedItem.Summary?.Trim().Substring(0, Math.Min(feedItem.Summary.Length, 2000)),
                    SourceUrl = feedItem.Url,
                    Publisher = GetPublisherFromSource(feedItem.Source),
                    PublishedAt = publishedAt,
                };

                // Enrich with source metadata
                EnrichArticleMetadata(article);

                // Try to classify event category based on title
                article.EventCategory = ClassifyEventFromTitle(article.Headline);

                _context.Articles.Add(article);
                articles.Add(article);
            }

            if (articles.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Ingested {Count} new articles from Alpha Vantage for ticker {Ticker}",
                    articles.Count, ticker);
            }

            return articles;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching news from Alpha Vantage for ticker {Ticker}", ticker);
            return new List<Article>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news from Alpha Vantage for ticker {Ticker}", ticker);
            return new List<Article>();
        }
    }

    public override async Task<bool> ValidateConfigurationAsync()
    {
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            // Test API with a simple call
            var apiKey = _configuration["DataSources:AlphaVantage:ApiKey"];
            var testUrl = $"https://www.alphavantage.co/query?function=NEWS_SENTIMENT&tickers=AAPL&apikey={apiKey}&limit=1";

            var response = await _httpClient.GetAsync(testUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string GetPublisherFromSource(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return "Alpha Vantage";
        }

        // Extract domain name from source
        if (source.Contains("bloomberg", StringComparison.OrdinalIgnoreCase))
            return "Bloomberg";
        if (source.Contains("reuters", StringComparison.OrdinalIgnoreCase))
            return "Reuters";
        if (source.Contains("wsj", StringComparison.OrdinalIgnoreCase) || source.Contains("wall street", StringComparison.OrdinalIgnoreCase))
            return "The Wall Street Journal";
        if (source.Contains("ft.com", StringComparison.OrdinalIgnoreCase) || source.Contains("financial times", StringComparison.OrdinalIgnoreCase))
            return "Financial Times";
        if (source.Contains("cnbc", StringComparison.OrdinalIgnoreCase))
            return "CNBC";
        if (source.Contains("marketwatch", StringComparison.OrdinalIgnoreCase))
            return "MarketWatch";

        return source;
    }

    private EventCategory ClassifyEventFromTitle(string title)
    {
        var lowerTitle = title.ToLower();

        // Earnings
        if (lowerTitle.Contains("earnings") || lowerTitle.Contains("eps") || lowerTitle.Contains("revenue"))
        {
            if (lowerTitle.Contains("beat") || lowerTitle.Contains("miss") || lowerTitle.Contains("exceeds") || lowerTitle.Contains("falls short"))
                return EventCategory.EarningsBeatMiss;
            if (lowerTitle.Contains("date") || lowerTitle.Contains("scheduled") || lowerTitle.Contains("call"))
                return EventCategory.EarningsCalendar;
        }

        // Guidance
        if (lowerTitle.Contains("guidance") || lowerTitle.Contains("forecast") || lowerTitle.Contains("outlook"))
            return EventCategory.GuidanceChange;

        // M&A
        if (lowerTitle.Contains("merger") || lowerTitle.Contains("acquisition") || lowerTitle.Contains("acquires") ||
            lowerTitle.Contains("partnership") || lowerTitle.Contains("deal"))
            return EventCategory.MergersAcquisitions;

        // Regulatory/Legal
        if (lowerTitle.Contains("sec") || lowerTitle.Contains("investigation") || lowerTitle.Contains("lawsuit") ||
            lowerTitle.Contains("regulatory") || lowerTitle.Contains("antitrust"))
            return EventCategory.RegulatoryLegal;

        // Leadership
        if (lowerTitle.Contains("ceo") || lowerTitle.Contains("cfo") || lowerTitle.Contains("resigns") ||
            lowerTitle.Contains("appoints") || lowerTitle.Contains("executive"))
            return EventCategory.LeadershipChange;

        // Layoffs
        if (lowerTitle.Contains("layoff") || lowerTitle.Contains("restructuring") || lowerTitle.Contains("job cuts"))
            return EventCategory.Layoffs;

        // Product Recall
        if (lowerTitle.Contains("recall") || lowerTitle.Contains("safety"))
            return EventCategory.ProductRecall;

        // Analyst Rating
        if (lowerTitle.Contains("upgrade") || lowerTitle.Contains("downgrade") || lowerTitle.Contains("analyst") ||
            lowerTitle.Contains("rating"))
            return EventCategory.AnalystRating;

        // Dividend/Buyback
        if (lowerTitle.Contains("dividend") || lowerTitle.Contains("buyback") || lowerTitle.Contains("share repurchase"))
            return EventCategory.DividendBuyback;

        // Product Launch
        if (lowerTitle.Contains("launch") || lowerTitle.Contains("unveils") || lowerTitle.Contains("announces new"))
            return EventCategory.ProductLaunch;

        // Contract Win
        if (lowerTitle.Contains("contract") || lowerTitle.Contains("wins") || lowerTitle.Contains("awarded"))
            return EventCategory.ContractWin;

        return EventCategory.Unknown;
    }
}

// Alpha Vantage API response models
public class AlphaVantageNewsResponse
{
    public string? Items { get; set; }
    public string? SentimentScoreDefinition { get; set; }
    public string? RelevanceScoreDefinition { get; set; }
    public List<AlphaVantageNewsFeed>? Feed { get; set; }
}

public class AlphaVantageNewsFeed
{
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? TimePublished { get; set; }
    public List<string>? Authors { get; set; }
    public string? Summary { get; set; }
    public string? BannerImage { get; set; }
    public string? Source { get; set; }
    public string? CategoryWithinSource { get; set; }
    public string? SourceDomain { get; set; }
    public List<AlphaVantageTickerSentiment>? TickerSentiment { get; set; }
}

public class AlphaVantageTickerSentiment
{
    public string? Ticker { get; set; }
    public string? RelevanceScore { get; set; }
    public string? TickerSentimentScore { get; set; }
    public string? TickerSentimentLabel { get; set; }
}
