using System.ComponentModel.DataAnnotations;

namespace SignalCopilot.Api.Models;

public class Impact
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int ArticleId { get; set; }

    [Required]
    public int HoldingId { get; set; }

    // Impact Score = direction × magnitude × confidence × exposure
    public decimal ImpactScore { get; set; }

    // Exposure: User's position weight for this ticker (0.0-1.0)
    [Range(0.0, 1.0)]
    public decimal Exposure { get; set; }

    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
    public Article Article { get; set; } = null!;
    public Holding Holding { get; set; } = null!;
}
