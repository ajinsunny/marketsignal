using SignalCopilot.Api.Models;

namespace SignalCopilot.Api.Services;

public interface ISentimentAnalyzer
{
    Task<Signal> AnalyzeArticleAsync(Article article);
    Task AnalyzeArticlesAsync(List<Article> articles);
}
