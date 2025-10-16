using System.ComponentModel.DataAnnotations;

namespace SignalCopilot.Api.Models;

public class Holding
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Ticker { get; set; } = string.Empty;

    [Required]
    public decimal Shares { get; set; }

    public decimal? CostBasis { get; set; }

    /// <summary>
    /// Date when position was first acquired (for holding period tracking)
    /// </summary>
    public DateTime? AcquiredAt { get; set; }

    /// <summary>
    /// User's intent/strategy for this holding (Trade, Accumulate, Income, Hold)
    /// </summary>
    public HoldingIntent Intent { get; set; } = HoldingIntent.Hold;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public ICollection<Impact> Impacts { get; set; } = new List<Impact>();
}
