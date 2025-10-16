using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

/// <summary>
/// Calculates consensus scores based on multi-source confirmation
/// Finds similar articles within a time window and computes stance agreement
/// </summary>
public interface IConsensusCalculator
{
    Task<ConsensusScore> CalculateConsensusAsync(Article article);
}

public class ConsensusCalculator : IConsensusCalculator
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConsensusCalculator> _logger;

    // Time window for consensus detection (6 hours)
    private const int ConsensusWindowHours = 6;

    public ConsensusCalculator(ApplicationDbContext context, ILogger<ConsensusCalculator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ConsensusScore> CalculateConsensusAsync(Article article)
    {
        // Find articles published within +/- 6 hours for the same ticker
        var windowStart = article.PublishedAt.AddHours(-ConsensusWindowHours);
        var windowEnd = article.PublishedAt.AddHours(ConsensusWindowHours);

        var relatedArticles = await _context.Articles
            .Include(a => a.Signal)
            .Where(a =>
                a.Ticker == article.Ticker &&
                a.PublishedAt >= windowStart &&
                a.PublishedAt <= windowEnd &&
                a.Id != article.Id &&
                a.Signal != null)
            .ToListAsync();

        if (!relatedArticles.Any())
        {
            // No consensus - single source
            return new ConsensusScore
            {
                UniqueSourceCount = 1,
                StanceAgreement = 1.0m,
                ConsensusFactor = 0.1m, // Low consensus penalty (single source)
                ConfidenceBonus = 0m
            };
        }

        // Count unique publishers (sources)
        var uniqueSources = relatedArticles
            .Select(a => a.Publisher?.ToLower() ?? "unknown")
            .Distinct()
            .Count() + 1; // +1 for current article

        // Calculate stance agreement (% of sources with same sentiment)
        var currentSignal = await _context.Signals.FirstOrDefaultAsync(s => s.ArticleId == article.Id);
        if (currentSignal == null)
        {
            return new ConsensusScore
            {
                UniqueSourceCount = uniqueSources,
                StanceAgreement = 1.0m,
                ConsensusFactor = Math.Min(uniqueSources / 10m, 1.0m),
                ConfidenceBonus = 0m
            };
        }

        var currentSentiment = currentSignal.Sentiment;
        var agreeingCount = relatedArticles.Count(a => a.Signal!.Sentiment == currentSentiment) + 1; // +1 for current
        var totalCount = relatedArticles.Count + 1;
        var stanceAgreement = (decimal)agreeingCount / totalCount;

        // Calculate consensus factor: (SourceCount / 10) * StanceAgreement, capped at 1.0
        var consensusFactor = Math.Min((uniqueSources / 10m) * stanceAgreement, 1.0m);

        // **PHASE 2 ENHANCEMENT: Increased consensus bonus for multi-source confirmation**
        // Confidence bonus: +0.15 if ≥3 sources, +0.10 if ≥2 sources, with high agreement (≥75%)
        var confidenceBonus = (uniqueSources >= 3 && stanceAgreement >= 0.75m) ? 0.15m :
                              (uniqueSources >= 2 && stanceAgreement >= 0.75m) ? 0.10m : 0m;

        _logger.LogInformation(
            "Consensus for {Ticker} article {ArticleId}: {Sources} sources, {Agreement:P0} agreement, factor={Factor:F2}",
            article.Ticker, article.Id, uniqueSources, stanceAgreement, consensusFactor);

        return new ConsensusScore
        {
            UniqueSourceCount = uniqueSources,
            StanceAgreement = stanceAgreement,
            ConsensusFactor = consensusFactor,
            ConfidenceBonus = confidenceBonus
        };
    }
}

/// <summary>
/// Consensus metrics for a specific article
/// </summary>
public class ConsensusScore
{
    public int UniqueSourceCount { get; set; }
    public decimal StanceAgreement { get; set; } // 0.0-1.0
    public decimal ConsensusFactor { get; set; } // 0.0-1.0
    public decimal ConfidenceBonus { get; set; } // 0.0-0.1
}
