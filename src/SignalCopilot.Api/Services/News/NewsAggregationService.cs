using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services.News;

/// <summary>
/// Aggregates news from multiple providers with deduplication
/// </summary>
public class NewsAggregationService
{
    private readonly IEnumerable<INewsProvider> _providers;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NewsAggregationService> _logger;

    public NewsAggregationService(
        IEnumerable<INewsProvider> providers,
        ApplicationDbContext context,
        ILogger<NewsAggregationService> logger)
    {
        _providers = providers;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Fetch and aggregate news from all available providers
    /// </summary>
    public async Task<List<Article>> FetchAndAggregateNewsAsync(string ticker, DateTime? fromDate = null)
    {
        var allArticles = new List<NewsArticleDto>();

        // Fetch from all available providers in parallel
        var fetchTasks = _providers
            .Where(p => p.IsAvailable())
            .Select(async provider =>
            {
                try
                {
                    return await provider.FetchNewsAsync(ticker, fromDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching news from provider {Provider} for ticker {Ticker}",
                        provider.ProviderName, ticker);
                    return new List<NewsArticleDto>();
                }
            });

        var results = await Task.WhenAll(fetchTasks);

        foreach (var articles in results)
        {
            allArticles.AddRange(articles);
        }

        if (!allArticles.Any())
        {
            _logger.LogInformation("No articles found for ticker {Ticker} from any provider", ticker);
            return new List<Article>();
        }

        // Deduplicate and persist
        var savedArticles = await DeduplicateAndPersistAsync(allArticles);

        _logger.LogInformation("Aggregated {TotalFetched} articles, saved {SavedCount} new articles for ticker {Ticker}",
            allArticles.Count, savedArticles.Count, ticker);

        return savedArticles;
    }

    /// <summary>
    /// Fetch news for multiple tickers
    /// </summary>
    public async Task<List<Article>> FetchAndAggregateNewsForTickersAsync(List<string> tickers, DateTime? fromDate = null)
    {
        var allArticles = new List<Article>();

        foreach (var ticker in tickers)
        {
            var articles = await FetchAndAggregateNewsAsync(ticker, fromDate);
            allArticles.AddRange(articles);
        }

        return allArticles;
    }

    /// <summary>
    /// Deduplicate articles by URL and headline similarity, then persist new ones
    /// </summary>
    private async Task<List<Article>> DeduplicateAndPersistAsync(List<NewsArticleDto> articles)
    {
        var savedArticles = new List<Article>();

        // Group by ticker for efficient database queries
        var byTicker = articles.GroupBy(a => a.Ticker);

        foreach (var group in byTicker)
        {
            var ticker = group.Key;

            // Get existing article URLs for this ticker
            var existingUrlsList = await _context.Articles
                .Where(a => a.Ticker == ticker)
                .Select(a => a.SourceUrl)
                .ToListAsync();

            var existingUrls = existingUrlsList.ToHashSet();

            foreach (var articleDto in group)
            {
                // Skip if URL already exists (exact duplicate)
                if (!string.IsNullOrEmpty(articleDto.SourceUrl) &&
                    existingUrls.Contains(articleDto.SourceUrl))
                {
                    continue;
                }

                // Additional headline similarity check could go here
                // For now, we trust URL-based deduplication

                var article = new Article
                {
                    Ticker = articleDto.Ticker.ToUpper(),
                    Headline = articleDto.Headline,
                    Summary = articleDto.Summary,
                    SourceUrl = articleDto.SourceUrl,
                    Publisher = articleDto.Publisher,
                    PublishedAt = articleDto.PublishedAt,
                    IngestedAt = DateTime.UtcNow,
                    SourceType = SourceType.News,
                    SourceTier = articleDto.SourceTier
                };

                _context.Articles.Add(article);
                savedArticles.Add(article);
                existingUrls.Add(articleDto.SourceUrl); // Track locally to avoid re-adding
            }
        }

        if (savedArticles.Any())
        {
            await _context.SaveChangesAsync();
        }

        return savedArticles;
    }

    /// <summary>
    /// Calculate headline similarity (for future advanced deduplication)
    /// </summary>
    private double CalculateHeadlineSimilarity(string headline1, string headline2)
    {
        // Simplified Jaccard similarity on word sets
        var words1 = headline1.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        var words2 = headline2.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        if (words1.Count == 0 && words2.Count == 0) return 1.0;
        if (words1.Count == 0 || words2.Count == 0) return 0.0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (double)intersection / union;
    }
}
