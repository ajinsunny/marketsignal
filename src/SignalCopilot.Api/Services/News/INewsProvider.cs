using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services.News;

/// <summary>
/// Raw news article data from a provider
/// </summary>
public class NewsArticleDto
{
    public required string Ticker { get; set; }
    public required string Headline { get; set; }
    public string? Summary { get; set; }
    public string? SourceUrl { get; set; }
    public required string Publisher { get; set; }
    public DateTime PublishedAt { get; set; }
    public SourceTier SourceTier { get; set; } = SourceTier.Unknown;
}

/// <summary>
/// Interface for news data providers
/// </summary>
public interface INewsProvider
{
    /// <summary>
    /// Provider name for logging/tracking
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Fetch news articles for a specific ticker symbol
    /// </summary>
    /// <param name="ticker">Stock ticker symbol</param>
    /// <param name="fromDate">Optional start date for articles</param>
    /// <returns>List of news articles</returns>
    Task<List<NewsArticleDto>> FetchNewsAsync(string ticker, DateTime? fromDate = null);

    /// <summary>
    /// Check if this provider is available (API key configured, etc.)
    /// </summary>
    bool IsAvailable();
}
