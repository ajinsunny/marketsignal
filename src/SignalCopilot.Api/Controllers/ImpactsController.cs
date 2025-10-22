using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalCopilot.Api.Data;
using System.Security.Claims;

namespace SignalCopilot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ImpactsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ImpactsController(ApplicationDbContext context)
    {
        _context = context;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated");
    }

    [HttpGet]
    public async Task<IActionResult> GetImpacts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] decimal? minImpactScore = null)
    {
        var userId = GetUserId();

        var query = _context.Impacts
            .AsNoTracking()
            .Include(i => i.Article)
            .Include(i => i.Holding)
            .Where(i => i.UserId == userId);

        if (minImpactScore.HasValue)
        {
            query = query.Where(i => Math.Abs(i.ImpactScore) >= minImpactScore.Value);
        }

        var totalCount = await query.CountAsync();

        var impacts = await query
            .OrderByDescending(i => Math.Abs(i.ImpactScore))
            .ThenByDescending(i => i.ComputedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id,
                i.ImpactScore,
                i.Exposure,
                i.ComputedAt,
                Article = new
                {
                    i.Article.Id,
                    i.Article.Ticker,
                    i.Article.Headline,
                    i.Article.Summary,
                    i.Article.SourceUrl,
                    i.Article.Publisher,
                    i.Article.PublishedAt,
                    i.Article.SourceTier
                },
                Holding = new
                {
                    i.Holding.Id,
                    i.Holding.Ticker,
                    i.Holding.Shares
                }
            })
            .ToListAsync();

        return Ok(new
        {
            impacts,
            pagination = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            }
        });
    }

    [HttpGet("high-impact")]
    public async Task<IActionResult> GetHighImpacts()
    {
        var userId = GetUserId();
        var threshold = 0.7m; // Can be configured from settings

        var impacts = await _context.Impacts
            .AsNoTracking()
            .Include(i => i.Article)
            .ThenInclude(a => a.Signal)
            .Include(i => i.Holding)
            .Where(i => i.UserId == userId && Math.Abs(i.ImpactScore) >= threshold)
            .OrderByDescending(i => Math.Abs(i.ImpactScore))
            .Take(10)
            .Select(i => new
            {
                i.Id,
                i.ImpactScore,
                i.Exposure,
                i.ComputedAt,
                Article = new
                {
                    i.Article.Id,
                    i.Article.Ticker,
                    i.Article.Headline,
                    i.Article.Summary,
                    i.Article.SourceUrl,
                    i.Article.Publisher,
                    i.Article.PublishedAt,
                    i.Article.SourceTier,
                    Signal = i.Article.Signal != null ? new
                    {
                        i.Article.Signal.Sentiment,
                        i.Article.Signal.Magnitude,
                        i.Article.Signal.Confidence,
                        i.Article.Signal.Reasoning
                    } : null
                },
                Holding = new
                {
                    i.Holding.Ticker,
                    i.Holding.Shares
                }
            })
            .ToListAsync();

        return Ok(impacts);
    }
}
