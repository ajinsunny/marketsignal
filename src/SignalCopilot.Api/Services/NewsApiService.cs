using System.Text.Json;
using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

public class NewsApiService : INewsService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NewsApiService> _logger;

    public NewsApiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<NewsApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    public async Task<List<Article>> FetchNewsForTickersAsync(List<string> tickers)
    {
        var allArticles = new List<Article>();

        foreach (var ticker in tickers)
        {
            var articles = await FetchNewsForTickerAsync(ticker);
            allArticles.AddRange(articles);
        }

        return allArticles;
    }

    public async Task<List<Article>> FetchNewsForTickerAsync(string ticker)
    {
        var apiKey = _configuration["NewsApi:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey == "your_newsapi_key_here")
        {
            _logger.LogWarning("NewsAPI key not configured. Skipping news fetch for {Ticker}", ticker);
            return new List<Article>();
        }

        try
        {
            // NewsAPI free tier only allows articles from last 30 days
            // Use recent date range (system shows October 2025, so using September 2025)
            var fromDate = "2025-09-20";
            var toDate = "2025-10-15";
            var url = $"https://newsapi.org/v2/everything?q={ticker}&from={fromDate}&to={toDate}&sortBy=publishedAt&language=en&apiKey={apiKey}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("NewsAPI returned {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var newsResponse = JsonSerializer.Deserialize<NewsApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (newsResponse?.Articles == null || !newsResponse.Articles.Any())
            {
                _logger.LogInformation("No articles found for ticker {Ticker}", ticker);
                return new List<Article>();
            }

            var articles = new List<Article>();

            foreach (var apiArticle in newsResponse.Articles.Take(10)) // Limit to 10 articles per ticker
            {
                // Check if article already exists
                var existingArticle = await _context.Articles
                    .FirstOrDefaultAsync(a => a.SourceUrl == apiArticle.Url);

                if (existingArticle != null)
                {
                    continue; // Skip duplicates
                }

                var article = new Article
                {
                    Ticker = ticker.ToUpper(),
                    Headline = apiArticle.Title?.Trim() ?? "No title",
                    Summary = apiArticle.Description?.Trim(),
                    SourceUrl = apiArticle.Url,
                    Publisher = apiArticle.Source?.Name,
                    PublishedAt = apiArticle.PublishedAt,
                    IngestedAt = DateTime.UtcNow
                };

                _context.Articles.Add(article);
                articles.Add(article);
            }

            if (articles.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Ingested {Count} new articles for ticker {Ticker}", articles.Count, ticker);
            }

            return articles;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching news for ticker {Ticker}", ticker);
            return new List<Article>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news for ticker {Ticker}", ticker);
            return new List<Article>();
        }
    }
}

// NewsAPI response models
public class NewsApiResponse
{
    public string? Status { get; set; }
    public int TotalResults { get; set; }
    public List<NewsApiArticle>? Articles { get; set; }
}

public class NewsApiArticle
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

public class NewsApiSource
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}
