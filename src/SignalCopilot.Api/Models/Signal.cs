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

    // Confidence: 0.0-1.0 score based on source credibility
    [Range(0.0, 1.0)]
    public decimal Confidence { get; set; }

    [MaxLength(1000)]
    public string? Reasoning { get; set; }

    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Article Article { get; set; } = null!;
}
