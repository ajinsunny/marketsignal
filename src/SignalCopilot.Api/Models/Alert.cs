using System.ComponentModel.DataAnnotations;

namespace SignalCopilot.Api.Models;

public enum AlertType
{
    DailyDigest,
    HighImpact
}

public enum AlertStatus
{
    Pending,
    Sent,
    Failed
}

public class Alert
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public AlertType Type { get; set; }

    [Required]
    public AlertStatus Status { get; set; } = AlertStatus.Pending;

    [MaxLength(200)]
    public string? Subject { get; set; }

    public string Content { get; set; } = string.Empty;

    // Comma-separated list of article IDs included in this alert
    [MaxLength(1000)]
    public string? ArticleIds { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public ApplicationUser User { get; set; } = null!;
}
