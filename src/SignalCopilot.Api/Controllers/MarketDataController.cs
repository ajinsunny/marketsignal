using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalCopilot.Api.Services;

namespace SignalCopilot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/market")]
public class MarketDataController : ControllerBase
{
    private readonly IMarketDataService _marketDataService;
    private readonly ILogger<MarketDataController> _logger;

    public MarketDataController(
        IMarketDataService marketDataService,
        ILogger<MarketDataController> logger)
    {
        _marketDataService = marketDataService;
        _logger = logger;
    }

    /// <summary>
    /// Get current market price for a ticker symbol
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., AAPL, TSLA)</param>
    /// <returns>Current price or 404 if ticker not found</returns>
    [HttpGet("price/{ticker}")]
    public async Task<IActionResult> GetPrice(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return BadRequest(new { message = "Ticker symbol is required" });
        }

        ticker = ticker.ToUpperInvariant().Trim();

        _logger.LogInformation("Price lookup requested for {Ticker}", ticker);

        var price = await _marketDataService.GetCurrentPriceAsync(ticker);

        if (price == null)
        {
            return NotFound(new { message = $"Price not found for ticker '{ticker}'. Please check the symbol and try again." });
        }

        return Ok(new
        {
            ticker,
            price = price.Value,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get detailed quote information for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol</param>
    [HttpGet("quote/{ticker}")]
    public async Task<IActionResult> GetQuote(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return BadRequest(new { message = "Ticker symbol is required" });
        }

        ticker = ticker.ToUpperInvariant().Trim();

        var quote = await _marketDataService.GetQuoteAsync(ticker);

        if (quote == null)
        {
            return NotFound(new { message = $"Quote not found for ticker '{ticker}'" });
        }

        return Ok(quote);
    }
}
