using Hangfire;
using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Data;

namespace SignalCopilot.Api.Services;

public class BackgroundJobsService
{
    public static void ConfigureRecurringJobs()
    {
        // News ingestion every 30 minutes
        RecurringJob.AddOrUpdate<BackgroundJobsService>(
            "fetch-news",
            x => x.FetchNewsForAllHoldingsAsync(),
            "*/30 * * * *"); // Every 30 minutes

        // Daily digest at 9 AM
        RecurringJob.AddOrUpdate<BackgroundJobsService>(
            "daily-digest",
            x => x.GenerateDailyDigestsAsync(),
            "0 9 * * *"); // 9 AM daily

        // High-impact alerts every hour
        RecurringJob.AddOrUpdate<BackgroundJobsService>(
            "high-impact-alerts",
            x => x.GenerateHighImpactAlertsAsync(),
            "0 * * * *"); // Every hour
    }

    private readonly ApplicationDbContext _context;
    private readonly INewsService _newsService;
    private readonly ISentimentAnalyzer _sentimentAnalyzer;
    private readonly IImpactCalculator _impactCalculator;
    private readonly IAlertService _alertService;
    private readonly ILogger<BackgroundJobsService> _logger;

    public BackgroundJobsService(
        ApplicationDbContext context,
        INewsService newsService,
        ISentimentAnalyzer sentimentAnalyzer,
        IImpactCalculator impactCalculator,
        IAlertService alertService,
        ILogger<BackgroundJobsService> logger)
    {
        _context = context;
        _newsService = newsService;
        _sentimentAnalyzer = sentimentAnalyzer;
        _impactCalculator = impactCalculator;
        _alertService = alertService;
        _logger = logger;
    }

    public async Task FetchNewsForAllHoldingsAsync()
    {
        _logger.LogInformation("Starting news fetch job");

        try
        {
            // Get all unique tickers from holdings
            var tickers = await _context.Holdings
                .Select(h => h.Ticker)
                .Distinct()
                .ToListAsync();

            if (!tickers.Any())
            {
                _logger.LogInformation("No holdings found, skipping news fetch");
                return;
            }

            _logger.LogInformation("Fetching news for {Count} tickers", tickers.Count);

            // Fetch news for all tickers
            var articles = await _newsService.FetchNewsForTickersAsync(tickers);

            _logger.LogInformation("Fetched {Count} new articles", articles.Count);

            if (articles.Any())
            {
                // Analyze sentiment for new articles
                await _sentimentAnalyzer.AnalyzeArticlesAsync(articles);

                // Calculate impacts
                foreach (var article in articles)
                {
                    await _impactCalculator.CalculateImpactsForArticleAsync(article);
                }

                _logger.LogInformation("Completed analysis and impact calculation for new articles");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in news fetch job");
            throw;
        }
    }

    public async Task GenerateDailyDigestsAsync()
    {
        _logger.LogInformation("Starting daily digest generation");

        try
        {
            await _alertService.GenerateDailyDigestsAsync();
            _logger.LogInformation("Daily digests generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily digests");
            throw;
        }
    }

    public async Task GenerateHighImpactAlertsAsync()
    {
        _logger.LogInformation("Starting high-impact alert generation");

        try
        {
            await _alertService.GenerateHighImpactAlertsAsync();
            _logger.LogInformation("High-impact alerts generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating high-impact alerts");
            throw;
        }
    }
}
