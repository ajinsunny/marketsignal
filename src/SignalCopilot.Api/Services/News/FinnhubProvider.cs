using SignalCopilot.Api.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalCopilot.Api.Services.News;

/// <summary>
/// Finnhub news provider - company news endpoint
/// Free tier: 60 calls/minute
/// </summary>
public class FinnhubProvider : INewsProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FinnhubProvider> _logger;
    private readonly string? _apiKey;
    private readonly string _baseUrl = "https://finnhub.io/api/v1";

    public string ProviderName => "Finnhub";

    private static readonly Dictionary<string, SourceTier> PublisherTierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Premium sources
        { "Bloomberg", SourceTier.Premium },
        { "Reuters", SourceTier.Premium },
        { "Wall Street Journal", SourceTier.Premium },
        { "WSJ", SourceTier.Premium },
        { "Financial Times", SourceTier.Premium },
        { "Barron's", SourceTier.Premium },

        // Standard sources
        { "CNBC", SourceTier.Standard },
        { "MarketWatch", SourceTier.Standard },
        { "Seeking Alpha", SourceTier.Standard },
        { "Yahoo Finance", SourceTier.Standard },
        { "The Motley Fool", SourceTier.Standard },
        { "Investor's Business Daily", SourceTier.Standard },
        { "Forbes", SourceTier.Standard },
        { "Business Insider", SourceTier.Standard },

        // Official sources
        { "PR Newswire", SourceTier.Official },
        { "Business Wire", SourceTier.Official },
        { "GlobeNewswire", SourceTier.Official }
    };

    public FinnhubProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FinnhubProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _apiKey = _configuration["Finnhub:ApiKey"];
    }

    public bool IsAvailable()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public async Task<List<NewsArticleDto>> FetchNewsAsync(string ticker, DateTime? fromDate = null)
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("Finnhub API key not configured");
            return new List<NewsArticleDto>();
        }

        try
        {
            var from = fromDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
            var to = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var url = $"{_baseUrl}/company-news?symbol={ticker}&from={from}&to={to}&token={_apiKey}";

            _logger.LogInformation("Fetching news from Finnhub for ticker {Ticker}", ticker);

            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Finnhub rate limit exceeded for ticker {Ticker}", ticker);
                return new List<NewsArticleDto>();
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Finnhub API error for ticker {Ticker}: {StatusCode} - {Error}",
                    ticker, response.StatusCode, errorContent);
                return new List<NewsArticleDto>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var articles = JsonSerializer.Deserialize<List<FinnhubNewsArticle>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (articles == null || !articles.Any())
            {
                _logger.LogInformation("No articles found from Finnhub for ticker {Ticker}", ticker);
                return new List<NewsArticleDto>();
            }

            var result = articles
                .Where(a => !string.IsNullOrEmpty(a.Headline) && !string.IsNullOrEmpty(a.Url))
                .Select(a => new NewsArticleDto
                {
                    Ticker = ticker.ToUpper(),
                    Headline = a.Headline,
                    Summary = a.Summary,
                    SourceUrl = a.Url,
                    Publisher = a.Source ?? "Unknown",
                    PublishedAt = DateTimeOffset.FromUnixTimeSeconds(a.Datetime).UtcDateTime,
                    SourceTier = DetermineSourceTier(a.Source)
                })
                .ToList();

            _logger.LogInformation("Fetched {Count} articles from Finnhub for ticker {Ticker}",
                result.Count, ticker);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news from Finnhub for ticker {Ticker}", ticker);
            return new List<NewsArticleDto>();
        }
    }

    private SourceTier DetermineSourceTier(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return SourceTier.Unknown;

        // Check exact match first
        if (PublisherTierMap.TryGetValue(source, out var tier))
            return tier;

        // Check if source contains any known publisher name
        foreach (var (publisher, publisherTier) in PublisherTierMap)
        {
            if (source.Contains(publisher, StringComparison.OrdinalIgnoreCase))
                return publisherTier;
        }

        // Default to Standard for unknown sources
        return SourceTier.Standard;
    }

    /// <summary>
    /// Finnhub API response model
    /// </summary>
    private class FinnhubNewsArticle
    {
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("datetime")]
        public long Datetime { get; set; }

        [JsonPropertyName("headline")]
        public string Headline { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("related")]
        public string? Related { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }
}
