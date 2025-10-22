using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using SignalCopilot.Api.Services;
using System.Security.Claims;
using Hangfire;

namespace SignalCopilot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HoldingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HoldingsController> _logger;

    public HoldingsController(
        ApplicationDbContext context,
        ILogger<HoldingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated");
    }

    [HttpGet]
    public async Task<IActionResult> GetHoldings()
    {
        var userId = GetUserId();
        var holdings = await _context.Holdings
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderBy(h => h.Ticker)
            .ToListAsync();

        return Ok(holdings);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetHolding(int id)
    {
        var userId = GetUserId();
        var holding = await _context.Holdings
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

        if (holding == null)
        {
            return NotFound();
        }

        return Ok(holding);
    }

    [HttpPost]
    public async Task<IActionResult> CreateHolding([FromBody] CreateHoldingRequest request)
    {
        var userId = GetUserId();

        // Check if holding already exists
        var existingHolding = await _context.Holdings
            .FirstOrDefaultAsync(h => h.UserId == userId && h.Ticker == request.Ticker.ToUpper());

        if (existingHolding != null)
        {
            return Conflict(new { message = "Holding for this ticker already exists" });
        }

        var holding = new Holding
        {
            UserId = userId,
            Ticker = request.Ticker.ToUpper(),
            Shares = request.Shares,
            CostBasis = request.CostBasis,
            AcquiredAt = request.AcquiredAt ?? DateTime.UtcNow,
            Intent = request.Intent ?? HoldingIntent.Hold,
            AddedAt = DateTime.UtcNow
        };

        _context.Holdings.Add(holding);
        await _context.SaveChangesAsync();

        // Schedule impact calculation as a background job to avoid timeout
        BackgroundJob.Enqueue<IImpactCalculator>(x => x.CalculateImpactsForUserAsync(userId));
        _logger.LogInformation("Scheduled impact calculation for new holding {Ticker} for user {UserId}", holding.Ticker, userId);

        return CreatedAtAction(nameof(GetHolding), new { id = holding.Id }, holding);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateHolding(int id, [FromBody] UpdateHoldingRequest request)
    {
        var userId = GetUserId();
        var holding = await _context.Holdings
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

        if (holding == null)
        {
            return NotFound();
        }

        holding.Shares = request.Shares;
        holding.CostBasis = request.CostBasis;

        if (request.AcquiredAt.HasValue)
        {
            holding.AcquiredAt = request.AcquiredAt.Value;
        }

        if (request.Intent.HasValue)
        {
            holding.Intent = request.Intent.Value;
        }

        holding.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(holding);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHolding(int id)
    {
        var userId = GetUserId();
        var holding = await _context.Holdings
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

        if (holding == null)
        {
            return NotFound();
        }

        _context.Holdings.Remove(holding);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public record CreateHoldingRequest(
    string Ticker,
    decimal Shares,
    decimal? CostBasis,
    DateTime? AcquiredAt = null,
    HoldingIntent? Intent = null
);

public record UpdateHoldingRequest(
    decimal Shares,
    decimal? CostBasis,
    DateTime? AcquiredAt = null,
    HoldingIntent? Intent = null
);
