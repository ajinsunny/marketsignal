using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Data;

namespace SignalCopilot.Api.Controllers;

/// <summary>
/// Health check endpoint for monitoring and deployment validation
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ApplicationDbContext context, ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Basic health check - returns 200 OK if API is running
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }

    /// <summary>
    /// Detailed health check - includes database connectivity
    /// </summary>
    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailed()
    {
        var health = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            checks = new Dictionary<string, object>()
        };

        // Check database connectivity
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            health.checks["database"] = new
            {
                status = canConnect ? "healthy" : "unhealthy",
                message = canConnect ? "Database connection successful" : "Cannot connect to database"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            health.checks["database"] = new
            {
                status = "unhealthy",
                message = ex.Message
            };
        }

        // Check if any component is unhealthy
        var anyUnhealthy = health.checks.Values
            .Cast<object>()
            .Any(c => ((dynamic)c).status == "unhealthy");

        if (anyUnhealthy)
        {
            return StatusCode(503, health);
        }

        return Ok(health);
    }
}
