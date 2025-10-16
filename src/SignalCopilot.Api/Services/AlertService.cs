using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

public class AlertService : IAlertService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertService> _logger;

    public AlertService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<AlertService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task GenerateHighImpactAlertsAsync()
    {
        var threshold = decimal.Parse(_configuration["AlertSettings:HighImpactThreshold"] ?? "0.7");

        // Get all users
        var users = await _context.Users.ToListAsync();

        foreach (var user in users)
        {
            // Get high-impact events for this user in the last 24 hours
            var highImpacts = await _context.Impacts
                .Include(i => i.Article)
                .ThenInclude(a => a.Signal)
                .Include(i => i.Holding)
                .Where(i => i.UserId == user.Id &&
                           Math.Abs(i.ImpactScore) >= threshold &&
                           i.ComputedAt >= DateTime.UtcNow.AddHours(-24))
                .OrderByDescending(i => Math.Abs(i.ImpactScore))
                .Take(5)
                .ToListAsync();

            if (!highImpacts.Any())
            {
                continue; // No high-impact events for this user
            }

            // Check if alert already exists for today
            var today = DateTime.UtcNow.Date;
            var existingAlert = await _context.Alerts
                .FirstOrDefaultAsync(a => a.UserId == user.Id &&
                                        a.Type == AlertType.HighImpact &&
                                        a.CreatedAt.Date == today);

            if (existingAlert != null)
            {
                continue; // Already sent high-impact alert today
            }

            // Create alert content
            var content = BuildHighImpactAlertContent(highImpacts);
            var articleIds = string.Join(",", highImpacts.Select(i => i.ArticleId));

            var alert = new Alert
            {
                UserId = user.Id,
                Type = AlertType.HighImpact,
                Status = AlertStatus.Pending,
                Subject = $"High Impact Alert: {highImpacts.Count} significant events affecting your portfolio",
                Content = content,
                ArticleIds = articleIds,
                CreatedAt = DateTime.UtcNow
            };

            _context.Alerts.Add(alert);

            _logger.LogInformation("Generated high-impact alert for user {UserId} with {Count} events",
                user.Id, highImpacts.Count);
        }

        await _context.SaveChangesAsync();
    }

    public async Task GenerateDailyDigestsAsync()
    {
        var users = await _context.Users.ToListAsync();

        foreach (var user in users)
        {
            // Get all impacts for this user in the last 24 hours
            var recentImpacts = await _context.Impacts
                .Include(i => i.Article)
                .ThenInclude(a => a.Signal)
                .Include(i => i.Holding)
                .Where(i => i.UserId == user.Id &&
                           i.ComputedAt >= DateTime.UtcNow.AddHours(-24))
                .OrderByDescending(i => Math.Abs(i.ImpactScore))
                .Take(20)
                .ToListAsync();

            if (!recentImpacts.Any())
            {
                _logger.LogInformation("No impacts to report for user {UserId}", user.Id);
                continue;
            }

            // Check if digest already sent today
            var today = DateTime.UtcNow.Date;
            var existingDigest = await _context.Alerts
                .FirstOrDefaultAsync(a => a.UserId == user.Id &&
                                        a.Type == AlertType.DailyDigest &&
                                        a.CreatedAt.Date == today);

            if (existingDigest != null)
            {
                continue;
            }

            var content = BuildDailyDigestContent(recentImpacts);
            var articleIds = string.Join(",", recentImpacts.Select(i => i.ArticleId));

            var alert = new Alert
            {
                UserId = user.Id,
                Type = AlertType.DailyDigest,
                Status = AlertStatus.Pending,
                Subject = $"Daily Portfolio Digest: {recentImpacts.Count} events",
                Content = content,
                ArticleIds = articleIds,
                CreatedAt = DateTime.UtcNow
            };

            _context.Alerts.Add(alert);

            _logger.LogInformation("Generated daily digest for user {UserId} with {Count} events",
                user.Id, recentImpacts.Count);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SendAlertAsync(int alertId)
    {
        var alert = await _context.Alerts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == alertId);

        if (alert == null)
        {
            _logger.LogWarning("Alert {AlertId} not found", alertId);
            return;
        }

        if (alert.Status != AlertStatus.Pending)
        {
            _logger.LogInformation("Alert {AlertId} already processed with status {Status}",
                alertId, alert.Status);
            return;
        }

        try
        {
            // TODO: Implement actual email sending via SendGrid
            // For now, just mark as sent and log
            _logger.LogInformation("Sending alert {AlertId} to user {Email}: {Subject}",
                alertId, alert.User.Email, alert.Subject);

            // Simulate email sending
            // await SendEmailAsync(alert.User.Email, alert.Subject, alert.Content);

            alert.Status = AlertStatus.Sent;
            alert.SentAt = DateTime.UtcNow;

            _logger.LogInformation("Alert {AlertId} sent successfully", alertId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert {AlertId}", alertId);
            alert.Status = AlertStatus.Failed;
            alert.ErrorMessage = ex.Message;
        }

        await _context.SaveChangesAsync();
    }

    private string BuildHighImpactAlertContent(List<Impact> impacts)
    {
        var content = "HIGH IMPACT EVENTS DETECTED\n\n";
        content += "The following significant events may affect your portfolio:\n\n";

        foreach (var impact in impacts)
        {
            var direction = impact.ImpactScore > 0 ? "POSITIVE" : "NEGATIVE";
            var article = impact.Article;
            var signal = article.Signal;

            content += $"[{direction}] {article.Ticker}: {article.Headline}\n";
            content += $"  Impact Score: {Math.Abs(impact.ImpactScore):F4}\n";
            content += $"  Sentiment: {GetSentimentText(signal!.Sentiment)}\n";
            content += $"  Magnitude: {signal.Magnitude}/3\n";
            content += $"  Your Exposure: {impact.Exposure:P1}\n";
            content += $"  Source: {article.Publisher}\n";
            content += $"  Published: {article.PublishedAt:g}\n";
            if (!string.IsNullOrEmpty(article.SourceUrl))
                content += $"  Link: {article.SourceUrl}\n";
            content += "\n";
        }

        content += "This is an automated alert. Not financial advice.\n";
        return content;
    }

    private string BuildDailyDigestContent(List<Impact> impacts)
    {
        var content = "DAILY PORTFOLIO DIGEST\n\n";
        content += $"Summary of {impacts.Count} events affecting your holdings:\n\n";

        var positiveCount = impacts.Count(i => i.ImpactScore > 0);
        var negativeCount = impacts.Count(i => i.ImpactScore < 0);

        content += $"Positive Events: {positiveCount}\n";
        content += $"Negative Events: {negativeCount}\n\n";

        content += "TOP EVENTS:\n\n";

        foreach (var impact in impacts.Take(10))
        {
            var direction = impact.ImpactScore > 0 ? "+" : "-";
            var article = impact.Article;

            content += $"{direction} {article.Ticker}: {article.Headline}\n";
            content += $"  Impact: {Math.Abs(impact.ImpactScore):F4} | Exposure: {impact.Exposure:P1}\n";
            content += $"  {article.Publisher} - {article.PublishedAt:g}\n\n";
        }

        content += "View full details in the app.\n";
        content += "This is an automated digest. Not financial advice.\n";
        return content;
    }

    private string GetSentimentText(int sentiment)
    {
        return sentiment switch
        {
            1 => "Positive",
            -1 => "Negative",
            _ => "Neutral"
        };
    }
}
