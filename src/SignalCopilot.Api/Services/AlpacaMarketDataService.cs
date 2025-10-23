using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalCopilot.Api.Services;

/// <summary>
/// Market data service using Alpaca Markets API
/// Free tier includes real-time data for US stocks
/// Docs: https://alpaca.markets/docs/api-references/market-data-api/
/// </summary>
public class AlpacaMarketDataService : IMarketDataService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AlpacaMarketDataService> _logger;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private const string BaseUrl = "https://data.alpaca.markets/v2";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public AlpacaMarketDataService(
        HttpClient httpClient,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<AlpacaMarketDataService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;

        _apiKey = configuration["Alpaca:ApiKey"] ?? throw new InvalidOperationException("Alpaca API Key not configured");
        _apiSecret = configuration["Alpaca:ApiSecret"] ?? throw new InvalidOperationException("Alpaca API Secret not configured");

        // Set up authentication headers
        _httpClient.DefaultRequestHeaders.Add("APCA-API-KEY-ID", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", _apiSecret);
    }

    public async Task<decimal?> GetCurrentPriceAsync(string ticker)
    {
        ticker = ticker.ToUpperInvariant().Trim();

        // Check cache first
        var cacheKey = $"price_{ticker}";
        if (_cache.TryGetValue<decimal>(cacheKey, out var cachedPrice))
        {
            _logger.LogDebug("Cache hit for {Ticker}: ${Price}", ticker, cachedPrice);
            return cachedPrice;
        }

        try
        {
            var quote = await GetQuoteAsync(ticker);
            if (quote != null)
            {
                // Cache the price
                _cache.Set(cacheKey, quote.CurrentPrice, CacheDuration);
                return quote.CurrentPrice;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price for {Ticker}", ticker);
            return null;
        }
    }

    public async Task<MarketQuote?> GetQuoteAsync(string ticker)
    {
        ticker = ticker.ToUpperInvariant().Trim();

        try
        {
            // Alpaca API endpoint for latest quote
            var url = $"{BaseUrl}/stocks/{ticker}/quotes/latest";

            _logger.LogInformation("Fetching quote from Alpaca for {Ticker}", ticker);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Alpaca API returned {StatusCode} for {Ticker}", response.StatusCode, ticker);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var alpacaResponse = JsonSerializer.Deserialize<AlpacaQuoteResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (alpacaResponse?.Quote == null)
            {
                _logger.LogWarning("No quote data found for {Ticker}", ticker);
                return null;
            }

            // Calculate current price from bid/ask midpoint
            var quote = alpacaResponse.Quote;
            var currentPrice = (quote.AskPrice + quote.BidPrice) / 2;

            return new MarketQuote
            {
                Ticker = ticker,
                CurrentPrice = currentPrice,
                DayOpen = null, // Not available in quote endpoint
                DayHigh = null,
                DayLow = null,
                PreviousClose = null,
                Volume = quote.AskSize + quote.BidSize,
                Timestamp = quote.Timestamp
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error fetching quote for {Ticker}", ticker);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing Alpaca response for {Ticker}", ticker);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching quote for {Ticker}", ticker);
            return null;
        }
    }

    // Alpaca API response models
    private class AlpacaQuoteResponse
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("quote")]
        public AlpacaQuote? Quote { get; set; }
    }

    private class AlpacaQuote
    {
        [JsonPropertyName("t")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("ax")]
        public string? AskExchange { get; set; }

        [JsonPropertyName("ap")]
        public decimal AskPrice { get; set; }

        [JsonPropertyName("as")]
        public long AskSize { get; set; }

        [JsonPropertyName("bx")]
        public string? BidExchange { get; set; }

        [JsonPropertyName("bp")]
        public decimal BidPrice { get; set; }

        [JsonPropertyName("bs")]
        public long BidSize { get; set; }

        [JsonPropertyName("c")]
        public List<string>? Conditions { get; set; }

        [JsonPropertyName("z")]
        public string? Tape { get; set; }
    }
}
