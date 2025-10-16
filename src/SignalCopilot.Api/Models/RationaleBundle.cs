namespace SignalCopilot.Api.Models;

/// <summary>
/// Structured input bundle for LLM-based recommendation generation
/// Provides all context needed to generate personalized, non-generic impact narratives
/// </summary>
public class RationaleBundle
{
    // Event context
    public string Ticker { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // e.g., "Analyst Downgrade", "Earnings Beat"
    public int Stance { get; set; } // -1, 0, 1
    public int Magnitude { get; set; } // 1-3
    public decimal Confidence { get; set; } // 0.0-1.0
    public string? EventDetails { get; set; } // e.g., "Target cut median -12%"

    // Consensus/multi-source data
    public ConsensusData? Consensus { get; set; }

    // User context
    public UserContext User { get; set; } = new();

    // Historical analogs (similar events in the past)
    public AnalogData? Analogs { get; set; }

    // Source quality
    public List<string> Sources { get; set; } = new();
    public bool IsRumor { get; set; }

    /// <summary>
    /// Generate a compact JSON summary for LLM prompts
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
}

/// <summary>
/// Multi-source consensus data for an event
/// </summary>
public class ConsensusData
{
    public int UniqueSourceCount { get; set; }
    public int UpgradesCount { get; set; }
    public int DowngradesCount { get; set; }
    public int WindowHours { get; set; } // Time window for consensus (e.g., 24h)
    public decimal StanceAgreement { get; set; } // 0.0-1.0 (% sources agreeing on sentiment)
}

/// <summary>
/// User-specific context for personalization
/// </summary>
public class UserContext
{
    public decimal ExposurePct { get; set; } // Position size as % of portfolio
    public decimal? CostBasis { get; set; }
    public decimal? UnrealizedPLPct { get; set; } // Unrealized P/L %
    public int? HoldingPeriodDays { get; set; }
    public string Horizon { get; set; } = "hold"; // "trade", "accumulate", "income", "hold"
    public string RiskProfile { get; set; } = "balanced"; // "conservative", "balanced", "aggressive"
    public decimal? CashBuffer { get; set; } // Available cash
    public bool IsConcentratedPosition { get; set; } // >15% exposure flag
}

/// <summary>
/// Historical analog events (similar past events for this ticker or sector)
/// </summary>
public class AnalogData
{
    public int Count { get; set; } // Number of similar events found
    public decimal MedianMove5D { get; set; } // Median 5-day price move (%)
    public decimal MedianMove30D { get; set; } // Median 30-day price move (%)
    public string? Pattern { get; set; } // e.g., "−3.8% median/5d after guidance cuts"
}
