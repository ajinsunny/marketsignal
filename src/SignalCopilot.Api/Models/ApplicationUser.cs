using Microsoft.AspNetCore.Identity;

namespace SignalCopilot.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string? Timezone { get; set; } = "UTC";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    public ICollection<Impact> Impacts { get; set; } = new List<Impact>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}
