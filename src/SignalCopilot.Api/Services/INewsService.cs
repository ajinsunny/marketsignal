using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services;

public interface INewsService
{
    Task<List<Article>> FetchNewsForTickersAsync(List<string> tickers);
    Task<List<Article>> FetchNewsForTickerAsync(string ticker);
}
