namespace SignalCopilot.Api.Services;

public interface IPortfolioAnalyzer
{
    Task<PortfolioAnalysisResult> AnalyzePortfolioAsync(string userId);
}

public class PortfolioAnalysisResult
{
    public List<RebalanceRecommendation> Recommendations { get; set; } = new();
    public DateTime AnalyzedAt { get; set; }
    public int TotalHoldings { get; set; }
    public int ImpactsAnalyzed { get; set; }
}

public class RebalanceRecommendation
{
    public string Ticker { get; set; } = string.Empty;
    public RecommendationType Action { get; set; }
    public string Suggestion { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> KeySignals { get; set; } = new();
    public string SourceTier { get; set; } = string.Empty;
    public double AverageImpactScore { get; set; }
    public int NewsCount { get; set; }
}

public enum RecommendationType
{
    StrongBuy,
    Buy,
    Hold,
    Sell,
    StrongSell
}
