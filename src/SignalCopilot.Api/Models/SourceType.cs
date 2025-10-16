namespace SignalCopilot.Api.Models;

/// <summary>
/// Type of data source for an article
/// </summary>
public enum SourceType
{
    /// <summary>
    /// Unknown or unclassified source
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// SEC filing (8-K, 10-Q, 10-K, etc.)
    /// </summary>
    SecFiling = 1,

    /// <summary>
    /// Company press release or IR announcement
    /// </summary>
    PressRelease = 2,

    /// <summary>
    /// News article from media outlet
    /// </summary>
    News = 3,

    /// <summary>
    /// Social media post from official company account
    /// </summary>
    Social = 4,

    /// <summary>
    /// Analyst report or rating
    /// </summary>
    AnalystReport = 5
}

/// <summary>
/// Quality tier of the source
/// </summary>
public enum SourceTier
{
    /// <summary>
    /// Unknown or unrated source
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Premium financial sources (Bloomberg, Reuters, WSJ, FT)
    /// </summary>
    Premium = 1,

    /// <summary>
    /// Standard news outlets (CNBC, MarketWatch, Yahoo Finance)
    /// </summary>
    Standard = 2,

    /// <summary>
    /// Social media or unverified sources
    /// </summary>
    Social = 3,

    /// <summary>
    /// Official company communications (press releases, SEC filings)
    /// </summary>
    Official = 4
}
