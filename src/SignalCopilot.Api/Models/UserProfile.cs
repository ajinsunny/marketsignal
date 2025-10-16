namespace SignalCopilot.Api.Models;

/// <summary>
/// User's risk tolerance profile - affects recommendation framing and alert thresholds
/// </summary>
public enum RiskProfile
{
    Conservative = 1,   // Risk-averse, capital preservation focus
    Balanced = 2,       // Moderate risk, balanced growth/safety
    Aggressive = 3      // High risk tolerance, growth focus
}

/// <summary>
/// User's intent for a specific holding - affects recommendation playbooks
/// </summary>
public enum HoldingIntent
{
    Trade = 1,          // Short-term position (days to weeks)
    Accumulate = 2,     // Building position over time (DCA, growth)
    Income = 3,         // Dividend/income focused (hold for yield)
    Hold = 4            // Long-term hold (buy and forget)
}
