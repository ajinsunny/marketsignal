namespace SignalCopilot.Api.Services;

public interface IAlertService
{
    Task GenerateHighImpactAlertsAsync();
    Task GenerateDailyDigestsAsync();
    Task SendAlertAsync(int alertId);
}
