using System.ComponentModel.DataAnnotations;

namespace SignalCopilot.Api.Models;

public class Signal
{
    public int Id { get; set; }

    [Required]
    public int ArticleId { get; set; }

    // Sentiment: -1 (negative), 0 (neutral), 1 (positive)
    [Range(-1, 1)]
    public int Sentiment { get; set; }

    // Magnitude: 1-3 scale (1=minor, 2=moderate, 3=major)
    [Range(1, 3)]
    public int Magnitude { get; set; }

    // Confidence: 0.0-1.0 score based on source credibility AND consensus
    [Range(0.0, 1.0)]
    public decimal Confidence { get; set; }

    [MaxLength(1000)]
    public string? Reasoning { get; set; }

    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    // Enhanced consensus and event classification fields

    /// <summary>
    /// Event category classification (derived from article or ML classifier)
    /// </summary>
    public EventCategory EventCategory { get; set; } = EventCategory.Unknown;

    /// <summary>
    /// Number of unique sources reporting this event (for clustered articles)
    /// Higher count = more reliable signal
    /// </summary>
    [Range(1, 100)]
    public int SourceCount { get; set; } = 1;

    /// <summary>
    /// Percentage of sources agreeing on sentiment (0.0-1.0)
    /// Example: 0.8 = 80% of sources agree on positive/negative stance
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal StanceAgreement { get; set; } = 1.0m;

    /// <summary>
    /// Consensus factor applied to confidence calculation
    /// Formula: (SourceCount / 10) * StanceAgreement, capped at 1.0
    /// </summary>
    [Range(0.0, 1.0)]
    public decimal ConsensusFactor { get; set; } = 1.0m;

    // Navigation properties
    public Article Article { get; set; } = null!;
}
