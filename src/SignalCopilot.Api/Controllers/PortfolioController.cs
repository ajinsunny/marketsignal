using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalCopilot.Api.Services;
using System.Security.Claims;
using Hangfire;

namespace SignalCopilot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly IImageProcessor _imageProcessor;
    private readonly IPortfolioAnalytics _portfolioAnalytics;
    private readonly ILogger<PortfolioController> _logger;

    public PortfolioController(
        IImageProcessor imageProcessor,
        IPortfolioAnalytics portfolioAnalytics,
        ILogger<PortfolioController> logger)
    {
        _imageProcessor = imageProcessor;
        _portfolioAnalytics = portfolioAnalytics;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated");
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadPortfolioImage([FromForm] IFormFile image)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "No image provided" });
        }

        // Validate file size (max 10MB)
        if (image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { message = "Image too large. Maximum size is 10MB." });
        }

        // Validate content type
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(image.ContentType.ToLower()))
        {
            return BadRequest(new { message = "Invalid image format. Allowed formats: JPEG, PNG, GIF, WebP" });
        }

        try
        {
            // Read image data
            byte[] imageData;
            using (var memoryStream = new MemoryStream())
            {
                await image.CopyToAsync(memoryStream);
                imageData = memoryStream.ToArray();
            }

            _logger.LogInformation("Processing uploaded image for ticker extraction. Size: {Size} bytes", imageData.Length);

            // Extract tickers from image
            var tickers = await _imageProcessor.ExtractTickersFromImageAsync(imageData, image.ContentType);

            if (!tickers.Any())
            {
                return Ok(new { tickers = new List<string>(), message = "No ticker symbols found in image" });
            }

            _logger.LogInformation("Successfully extracted {Count} tickers: {Tickers}",
                tickers.Count, string.Join(", ", tickers));

            return Ok(new { tickers });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing portfolio image");
            return StatusCode(500, new { message = "Error processing image. Please try again." });
        }
    }

    // PHASE 4B: Portfolio Analytics Endpoints

    /// <summary>
    /// Get overall portfolio metrics (concentration, exposures, etc.)
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetPortfolioMetrics()
    {
        try
        {
            var userId = GetUserId();
            var metrics = await _portfolioAnalytics.GetPortfolioMetricsAsync(userId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio metrics");
            return StatusCode(500, new { message = "Error retrieving portfolio metrics" });
        }
    }

    /// <summary>
    /// Get intent-specific metrics (Trade, Accumulate, Income, Hold)
    /// </summary>
    [HttpGet("intent-metrics")]
    public async Task<IActionResult> GetIntentMetrics()
    {
        try
        {
            var userId = GetUserId();
            var metrics = await _portfolioAnalytics.GetAllIntentMetricsAsync(userId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving intent metrics");
            return StatusCode(500, new { message = "Error retrieving intent metrics" });
        }
    }

    /// <summary>
    /// Get performance metrics for a specific holding
    /// </summary>
    [HttpGet("holding-performance/{holdingId}")]
    public async Task<IActionResult> GetHoldingPerformance(int holdingId)
    {
        try
        {
            var performance = await _portfolioAnalytics.GetHoldingPerformanceAsync(holdingId);
            return Ok(performance);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving holding performance for {HoldingId}", holdingId);
            return StatusCode(500, new { message = "Error retrieving holding performance" });
        }
    }
}
