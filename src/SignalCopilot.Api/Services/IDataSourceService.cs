using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services;

/// <summary>
/// Interface for pluggable data sources (NewsAPI, SEC Edgar, Alpha Vantage, etc.)
/// Each implementation fetches articles from a specific source and enriches them with metadata
/// </summary>
public interface IDataSourceService
{
    /// <summary>
    /// Name of the data source (e.g., "NewsAPI", "SEC Edgar", "Alpha Vantage")
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Type of source (News, SecFiling, PressRelease, Social, etc.)
    /// </summary>
    SourceType SourceType { get; }

    /// <summary>
    /// Quality tier of this source (Premium, Standard, Social, Official)
    /// </summary>
    SourceTier SourceTier { get; }

    /// <summary>
    /// Whether this data source is currently enabled in configuration
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Fetch news/articles for a specific ticker symbol
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL", "GOOGL")</param>
    /// <returns>List of articles from this source</returns>
    Task<List<Article>> FetchForTickerAsync(string ticker);

    /// <summary>
    /// Fetch news/articles for multiple tickers (batch operation)
    /// </summary>
    /// <param name="tickers">List of ticker symbols</param>
    /// <returns>List of articles from this source</returns>
    Task<List<Article>> FetchForTickersAsync(List<string> tickers);

    /// <summary>
    /// Validate that the data source is properly configured (API keys, etc.)
    /// </summary>
    /// <returns>True if configuration is valid</returns>
    Task<bool> ValidateConfigurationAsync();
}

/// <summary>
/// Base class for data source implementations with common functionality
/// </summary>
public abstract class DataSourceServiceBase : IDataSourceService
{
    protected readonly IConfiguration _configuration;
    protected readonly ILogger _logger;

    public abstract string SourceName { get; }
    public abstract SourceType SourceType { get; }
    public abstract SourceTier SourceTier { get; }
    public abstract bool IsEnabled { get; }

    protected DataSourceServiceBase(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public abstract Task<List<Article>> FetchForTickerAsync(string ticker);

    public virtual async Task<List<Article>> FetchForTickersAsync(List<string> tickers)
    {
        var allArticles = new List<Article>();

        foreach (var ticker in tickers)
        {
            var articles = await FetchForTickerAsync(ticker);
            allArticles.AddRange(articles);
        }

        return allArticles;
    }

    public abstract Task<bool> ValidateConfigurationAsync();

    /// <summary>
    /// Helper method to enrich an article with source metadata
    /// </summary>
    protected void EnrichArticleMetadata(Article article)
    {
        article.SourceType = SourceType;
        article.SourceTier = SourceTier;
        article.IngestedAt = DateTime.UtcNow;
    }
}
