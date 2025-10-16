namespace SignalCopilot.Api.Models;

/// <summary>
/// Categorizes news events with predefined magnitude priors.
/// This taxonomy helps determine the potential impact severity of different event types.
/// </summary>
public enum EventCategory
{
    /// <summary>
    /// Default for uncategorized events (Magnitude: 1 - Minor)
    /// </summary>
    Unknown = 0,

    // MAJOR EVENTS (Magnitude: 3)

    /// <summary>
    /// Company guidance change, outlook revision (Magnitude: 3 - Major)
    /// Examples: "Company raises FY guidance", "CEO lowers revenue forecast"
    /// </summary>
    GuidanceChange = 3,

    /// <summary>
    /// Quarterly earnings beat or miss expectations (Magnitude: 3 - Major)
    /// Examples: "Q2 EPS beats by $0.15", "Revenue misses estimates"
    /// </summary>
    EarningsBeatMiss = 13,

    /// <summary>
    /// Regulatory action, legal issues, investigations (Magnitude: 3 - Major)
    /// Examples: "SEC opens investigation", "Antitrust lawsuit filed"
    /// </summary>
    RegulatoryLegal = 23,

    /// <summary>
    /// Mergers, acquisitions, strategic partnerships (Magnitude: 3 - Major)
    /// Examples: "Company acquires competitor for $2B", "Strategic partnership announced"
    /// </summary>
    MergersAcquisitions = 33,

    // MODERATE EVENTS (Magnitude: 2)

    /// <summary>
    /// Product recall, safety issues (Magnitude: 2 - Moderate)
    /// Examples: "Tesla recalls 100k vehicles", "FDA warns about drug side effects"
    /// </summary>
    ProductRecall = 2,

    /// <summary>
    /// C-suite leadership change (Magnitude: 2 - Moderate)
    /// Examples: "CEO resigns", "New CFO appointed"
    /// </summary>
    LeadershipChange = 12,

    /// <summary>
    /// Significant layoffs (>10% workforce) (Magnitude: 2 - Moderate)
    /// Examples: "Company lays off 15% of staff", "Restructuring announced"
    /// </summary>
    Layoffs = 22,

    /// <summary>
    /// Macro or sector-wide events (Magnitude: 2 - Moderate)
    /// Examples: "Fed raises rates", "Chip shortage worsens", "Oil prices surge"
    /// </summary>
    MacroSectorShock = 32,

    /// <summary>
    /// Contract wins, major customer deals (Magnitude: 2 - Moderate)
    /// Examples: "Wins $500M government contract", "Signs multi-year partnership"
    /// </summary>
    ContractWin = 42,

    // MINOR EVENTS (Magnitude: 1)

    /// <summary>
    /// Dividend announcement, share buyback program (Magnitude: 1 - Minor)
    /// Examples: "Increases dividend by 5%", "Approves $1B buyback"
    /// </summary>
    DividendBuyback = 1,

    /// <summary>
    /// New product launch, feature announcement (Magnitude: 1 - Minor)
    /// Examples: "Launches new smartphone model", "Adds AI features"
    /// </summary>
    ProductLaunch = 11,

    /// <summary>
    /// Analyst rating change (upgrade/downgrade) (Magnitude: 1 - Minor)
    /// Examples: "Goldman upgrades to Buy", "Morgan Stanley downgrades"
    /// </summary>
    AnalystRating = 21,

    /// <summary>
    /// Earnings date scheduled, conference participation (Magnitude: 1 - Minor)
    /// Examples: "Q3 earnings call scheduled for Oct 15", "CEO to speak at conference"
    /// </summary>
    EarningsCalendar = 31
}

/// <summary>
/// Extension methods for EventCategory
/// </summary>
public static class EventCategoryExtensions
{
    /// <summary>
    /// Gets the default magnitude for this event category
    /// </summary>
    public static int GetDefaultMagnitude(this EventCategory category)
    {
        return category switch
        {
            // Major (3)
            EventCategory.GuidanceChange => 3,
            EventCategory.EarningsBeatMiss => 3,
            EventCategory.RegulatoryLegal => 3,
            EventCategory.MergersAcquisitions => 3,

            // Moderate (2)
            EventCategory.ProductRecall => 2,
            EventCategory.LeadershipChange => 2,
            EventCategory.Layoffs => 2,
            EventCategory.MacroSectorShock => 2,
            EventCategory.ContractWin => 2,

            // Minor (1)
            EventCategory.DividendBuyback => 1,
            EventCategory.ProductLaunch => 1,
            EventCategory.AnalystRating => 1,
            EventCategory.EarningsCalendar => 1,

            // Unknown
            _ => 1
        };
    }

    /// <summary>
    /// Gets a human-readable description of the event category
    /// </summary>
    public static string GetDescription(this EventCategory category)
    {
        return category switch
        {
            EventCategory.GuidanceChange => "Guidance Change",
            EventCategory.EarningsBeatMiss => "Earnings Beat/Miss",
            EventCategory.RegulatoryLegal => "Regulatory/Legal",
            EventCategory.MergersAcquisitions => "M&A",
            EventCategory.ProductRecall => "Product Recall",
            EventCategory.LeadershipChange => "Leadership Change",
            EventCategory.Layoffs => "Layoffs",
            EventCategory.MacroSectorShock => "Macro/Sector",
            EventCategory.ContractWin => "Contract Win",
            EventCategory.DividendBuyback => "Dividend/Buyback",
            EventCategory.ProductLaunch => "Product Launch",
            EventCategory.AnalystRating => "Analyst Rating",
            EventCategory.EarningsCalendar => "Earnings Calendar",
            _ => "Unknown"
        };
    }
}
