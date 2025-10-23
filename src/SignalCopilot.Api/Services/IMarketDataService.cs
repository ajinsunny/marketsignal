namespace SignalCopilot.Api.Services;

/// <summary>
/// Service for fetching real-time market data (current prices, quotes, etc.)
/// </summary>
public interface IMarketDataService
{
    /// <summary>
    /// Get the current market price for a ticker symbol
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., AAPL, TSLA)</param>
    /// <returns>Current price or null if ticker not found</returns>
    Task<decimal?> GetCurrentPriceAsync(string ticker);

    /// <summary>
    /// Get detailed quote information for a ticker
    /// </summary>
    Task<MarketQuote?> GetQuoteAsync(string ticker);
}

/// <summary>
/// Market quote data
/// </summary>
public class MarketQuote
{
    public string Ticker { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal? DayOpen { get; set; }
    public decimal? DayHigh { get; set; }
    public decimal? DayLow { get; set; }
    public decimal? PreviousClose { get; set; }
    public long? Volume { get; set; }
    public DateTime Timestamp { get; set; }
}
