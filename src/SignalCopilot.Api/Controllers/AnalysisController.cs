using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalCopilot.Api.Services;
using System.Security.Claims;

namespace SignalCopilot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly IPortfolioAnalyzer _portfolioAnalyzer;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IPortfolioAnalyzer portfolioAnalyzer,
        ILogger<AnalysisController> logger)
    {
        _portfolioAnalyzer = portfolioAnalyzer;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated");
    }

    /// <summary>
    /// Get portfolio rebalancing recommendations based on impact analysis
    /// </summary>
    [HttpGet("rebalance-suggestions")]
    public async Task<IActionResult> GetRebalanceSuggestions()
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Getting rebalance suggestions for user {UserId}", userId);

            var analysis = await _portfolioAnalyzer.AnalyzePortfolioAsync(userId);

            return Ok(analysis);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rebalance suggestions");
            return StatusCode(500, new { message = "Error analyzing portfolio" });
        }
    }
}
