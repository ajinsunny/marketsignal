using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services;

public interface IImpactCalculator
{
    Task CalculateImpactsForArticleAsync(Article article);
    Task CalculateImpactsForUserAsync(string userId);
    Task CalculateAllImpactsAsync();
}
