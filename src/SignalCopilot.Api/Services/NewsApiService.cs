using SignalCopilot.Api.Models;
using SignalCopilot.Api.Services.News;

namespace SignalCopilot.Api.Services;

/// <summary>
/// News service that aggregates from multiple providers
/// </summary>
public class NewsApiService : INewsService
{
    private readonly NewsAggregationService _aggregationService;
    private readonly ILogger<NewsApiService> _logger;

    public NewsApiService(
        NewsAggregationService aggregationService,
        ILogger<NewsApiService> logger)
    {
        _aggregationService = aggregationService;
        _logger = logger;
    }

    public async Task<List<Article>> FetchNewsForTickersAsync(List<string> tickers)
    {
        return await _aggregationService.FetchAndAggregateNewsForTickersAsync(tickers);
    }

    public async Task<List<Article>> FetchNewsForTickerAsync(string ticker)
    {
        return await _aggregationService.FetchAndAggregateNewsAsync(ticker);
    }
}
