using System.ComponentModel.DataAnnotations;

namespace SignalCopilot.Api.Models;

public class Article
{
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    public string Ticker { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Headline { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? SourceUrl { get; set; }

    [MaxLength(100)]
    public string? Publisher { get; set; }

    public DateTime PublishedAt { get; set; }

    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;

    // Enhanced metadata for signal quality improvements

    /// <summary>
    /// Type of data source (SEC Filing, Press Release, News, Social, Analyst)
    /// </summary>
    public SourceType SourceType { get; set; } = SourceType.News;

    /// <summary>
    /// Quality tier of the source (Premium, Standard, Social, Official)
    /// </summary>
    public SourceTier SourceTier { get; set; } = SourceTier.Standard;

    /// <summary>
    /// Event category classification for magnitude priors
    /// </summary>
    public EventCategory EventCategory { get; set; } = EventCategory.Unknown;

    /// <summary>
    /// Cluster ID for de-duplication (same event from multiple sources)
    /// Format: "yyyyMMdd-{hash}" e.g., "20251016-abc123"
    /// </summary>
    [MaxLength(50)]
    public string? ClusterId { get; set; }

    /// <summary>
    /// Related tickers for multi-company events (JSON array)
    /// Example: ["AAPL", "GOOGL"] for supply chain or sector news
    /// </summary>
    [MaxLength(500)]
    public string? RelatedTickers { get; set; }

    // Navigation properties
    public Signal? Signal { get; set; }
    public ICollection<Impact> Impacts { get; set; } = new List<Impact>();
}
