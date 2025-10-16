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

    // Navigation properties
    public Signal? Signal { get; set; }
    public ICollection<Impact> Impacts { get; set; } = new List<Impact>();
}
