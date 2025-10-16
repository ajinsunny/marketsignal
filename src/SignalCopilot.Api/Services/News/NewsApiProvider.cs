using System.Text.Json;
using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services.News;

/// <summary>
/// NewsAPI.org provider implementation
/// </summary>
public class NewsApiProvider : INewsProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NewsApiProvider> _logger;

    // Publisher name to source tier mapping
    private static readonly Dictionary<string, SourceTier> PublisherTierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Premium: Premium financial sources
        { "Bloomberg", SourceTier.Premium },
        { "The Wall Street Journal", SourceTier.Premium },
        { "Financial Times", SourceTier.Premium },
        { "WSJ", SourceTier.Premium },
        { "Barron's", SourceTier.Premium },
        { "Reuters", SourceTier.Premium },

        // Standard: Established financial news
        { "CNBC", SourceTier.Standard },
        { "MarketWatch", SourceTier.Standard },
        { "Seeking Alpha", SourceTier.Standard },
        { "The Motley Fool", SourceTier.Standard },
        { "Investor's Business Daily", SourceTier.Standard },
        { "Yahoo Finance", SourceTier.Standard },

        // Social/Unknown is default for unrecognized sources
    };

    public NewsApiProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NewsApiProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public string ProviderName => "NewsAPI";

    public bool IsAvailable()
    {
        var apiKey = _configuration["NewsApi:ApiKey"];
        return !string.IsNullOrEmpty(apiKey) && apiKey != "your_newsapi_key_here";
    }

    public async Task<List<NewsArticleDto>> FetchNewsAsync(string ticker, DateTime? fromDate = null)
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("NewsAPI key not configured. Skipping news fetch for {Ticker}", ticker);
            return new List<NewsArticleDto>();
        }

        try
        {
            var apiKey = _configuration["NewsApi:ApiKey"];

            // NewsAPI free tier only allows articles from last 30 days
            var from = fromDate ?? DateTime.UtcNow.AddDays(-7);
            var to = DateTime.UtcNow;

            var fromStr = from.ToString("yyyy-MM-dd");
            var toStr = to.ToString("yyyy-MM-dd");

            var url = $"https://newsapi.org/v2/everything?q={ticker}&from={fromStr}&to={toStr}&sortBy=publishedAt&language=en&apiKey={apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("NewsAPI returned {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return new List<NewsArticleDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var newsResponse = JsonSerializer.Deserialize<NewsApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (newsResponse?.Articles == null || !newsResponse.Articles.Any())
            {
                _logger.LogInformation("No articles found for ticker {Ticker} from {Provider}", ticker, ProviderName);
                return new List<NewsArticleDto>();
            }

            var articles = newsResponse.Articles
                .Take(10) // Limit to 10 articles per ticker
                .Select(a => new NewsArticleDto
                {
                    Ticker = ticker.ToUpper(),
                    Headline = a.Title?.Trim() ?? "No title",
                    Summary = a.Description?.Trim(),
                    SourceUrl = a.Url,
                    Publisher = a.Source?.Name ?? "Unknown",
                    PublishedAt = a.PublishedAt,
                    SourceTier = DetermineSourceTier(a.Source?.Name)
                })
                .ToList();

            _logger.LogInformation("Fetched {Count} articles for {Ticker} from {Provider}",
                articles.Count, ticker, ProviderName);

            return articles;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching news for ticker {Ticker} from {Provider}", ticker, ProviderName);
            return new List<NewsArticleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news for ticker {Ticker} from {Provider}", ticker, ProviderName);
            return new List<NewsArticleDto>();
        }
    }

    private SourceTier DetermineSourceTier(string? publisherName)
    {
        if (string.IsNullOrEmpty(publisherName))
        {
            return SourceTier.Unknown;
        }

        // Try exact match first
        if (PublisherTierMap.TryGetValue(publisherName, out var tier))
        {
            return tier;
        }

        // Try partial match (e.g., "WSJ - News" contains "WSJ")
        foreach (var (key, value) in PublisherTierMap)
        {
            if (publisherName.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return SourceTier.Unknown;
    }
}

// NewsAPI response models
internal class NewsApiResponse
{
    public string? Status { get; set; }
    public int TotalResults { get; set; }
    public List<NewsApiArticle>? Articles { get; set; }
}

internal class NewsApiArticle
{
    public NewsApiSource? Source { get; set; }
    public string? Author { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? UrlToImage { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? Content { get; set; }
}

internal class NewsApiSource
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}
