using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignalCopilot.Api.Services;
using Hangfire;

namespace SignalCopilot.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IBackgroundJobClient backgroundJobClient,
        ILogger<JobsController> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    [HttpPost("fetch-news")]
    public IActionResult TriggerNewsFetch()
    {
        var jobId = _backgroundJobClient.Enqueue<BackgroundJobsService>(
            x => x.FetchNewsForAllHoldingsAsync());

        _logger.LogInformation("News fetch job triggered with ID: {JobId}", jobId);

        return Ok(new { message = "News fetch job triggered", jobId });
    }

    [HttpPost("generate-alerts")]
    public IActionResult TriggerAlertGeneration()
    {
        var jobId = _backgroundJobClient.Enqueue<BackgroundJobsService>(
            x => x.GenerateHighImpactAlertsAsync());

        _logger.LogInformation("Alert generation job triggered with ID: {JobId}", jobId);

        return Ok(new { message = "Alert generation job triggered", jobId });
    }

    [HttpPost("generate-digests")]
    public IActionResult TriggerDigestGeneration()
    {
        var jobId = _backgroundJobClient.Enqueue<BackgroundJobsService>(
            x => x.GenerateDailyDigestsAsync());

        _logger.LogInformation("Digest generation job triggered with ID: {JobId}", jobId);

        return Ok(new { message = "Digest generation job triggered", jobId });
    }

    [HttpGet("status")]
    public IActionResult GetJobStatus()
    {
        return Ok(new
        {
            message = "Jobs API is running. Use POST endpoints to trigger jobs manually.",
            endpoints = new[]
            {
                "/api/jobs/fetch-news",
                "/api/jobs/generate-alerts",
                "/api/jobs/generate-digests"
            }
        });
    }
}
