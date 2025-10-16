using System.Text.Json;
using System.Text.RegularExpressions;
using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SignalCopilot.Api.Services;

/// <summary>
/// SEC EDGAR API integration for official company filings (8-K, 10-Q, 10-K)
/// Free API, no key required, just requires User-Agent with contact email
/// Documentation: https://www.sec.gov/edgar/sec-api-documentation
/// Rate limit: 10 requests per second
/// </summary>
public class SecEdgarService : DataSourceServiceBase
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private const string BaseUrl = "https://data.sec.gov";

    public override string SourceName => "SEC EDGAR";
    public override SourceType SourceType => SourceType.SecFiling;
    public override SourceTier SourceTier => SourceTier.Official; // Official company disclosures

    public override bool IsEnabled =>
        !string.IsNullOrEmpty(_configuration["DataSources:SecEdgar:ContactEmail"]) &&
        _configuration.GetValue<bool>("DataSources:SecEdgar:Enabled", true); // Enabled by default since it's free

    public SecEdgarService(
        HttpClient httpClient,
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<SecEdgarService> logger)
        : base(configuration, logger)
    {
        _httpClient = httpClient;
        _context = context;

        // SEC requires User-Agent with contact information
        var contactEmail = _configuration["DataSources:SecEdgar:ContactEmail"] ?? "noreply@signalcopilot.com";
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"SignalCopilot/1.0 ({contactEmail})");
    }

    public override async Task<List<Article>> FetchForTickerAsync(string ticker)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("SEC EDGAR is not enabled. Check configuration.");
            return new List<Article>();
        }

        try
        {
            // First, get the CIK (Central Index Key) for this ticker
            var cik = await GetCikForTickerAsync(ticker);
            if (string.IsNullOrEmpty(cik))
            {
                _logger.LogWarning("Could not find CIK for ticker {Ticker}", ticker);
                return new List<Article>();
            }

            _logger.LogInformation("Fetching SEC filings for ticker {Ticker} (CIK: {CIK})", ticker, cik);

            // Fetch recent 8-K filings (material events)
            var filings = await FetchRecentFilingsAsync(cik, ticker);

            return filings;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching SEC filings for ticker {Ticker}", ticker);
            return new List<Article>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching SEC filings for ticker {Ticker}", ticker);
            return new List<Article>();
        }
    }

    private async Task<string?> GetCikForTickerAsync(string ticker)
    {
        try
        {
            // Use the company tickers JSON endpoint
            var url = $"{BaseUrl}/files/company_tickers.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var tickersData = JsonSerializer.Deserialize<Dictionary<string, SecCompanyInfo>>(content);

            if (tickersData == null)
            {
                return null;
            }

            // Find the company by ticker
            var company = tickersData.Values.FirstOrDefault(c =>
                string.Equals(c.Ticker, ticker, StringComparison.OrdinalIgnoreCase));

            if (company != null)
            {
                // Pad CIK to 10 digits
                return company.CikStr.ToString().PadLeft(10, '0');
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching CIK for ticker {Ticker}", ticker);
            return null;
        }
    }

    private async Task<List<Article>> FetchRecentFilingsAsync(string cik, string ticker)
    {
        try
        {
            // Fetch submissions data for this company
            var url = $"{BaseUrl}/submissions/CIK{cik}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SEC EDGAR returned {StatusCode} for CIK {CIK}", response.StatusCode, cik);
                return new List<Article>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var submissionsData = JsonSerializer.Deserialize<SecSubmissionsResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (submissionsData?.Filings?.Recent == null)
            {
                return new List<Article>();
            }

            var articles = new List<Article>();
            var recent = submissionsData.Filings.Recent;

            // Process 8-K filings (material events) from the last 30 days
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            for (int i = 0; i < recent.AccessionNumber.Count && i < 20; i++) // Limit to 20 filings
            {
                var form = recent.Form[i];

                // Focus on 8-K (material events), 10-Q (quarterly), 10-K (annual)
                if (form != "8-K" && form != "10-Q" && form != "10-K")
                {
                    continue;
                }

                // Parse filing date
                if (!DateTime.TryParseExact(recent.FilingDate[i], "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var filingDate))
                {
                    continue;
                }

                if (filingDate < thirtyDaysAgo)
                {
                    continue; // Skip old filings
                }

                var accessionNumber = recent.AccessionNumber[i].Replace("-", "");
                var documentUrl = $"https://www.sec.gov/cgi-bin/viewer?action=view&cik={cik}&accession_number={recent.AccessionNumber[i]}&xbrl_type=v";

                // Check if article already exists by URL
                var existingArticle = await _context.Articles
                    .FirstOrDefaultAsync(a => a.SourceUrl == documentUrl);

                if (existingArticle != null)
                {
                    continue; // Skip duplicates
                }

                // Create headline and summary based on filing type
                var (headline, summary, eventCategory) = GenerateFilingSummary(form, ticker, recent.PrimaryDocument[i]);

                var article = new Article
                {
                    Ticker = ticker.ToUpper(),
                    Headline = headline,
                    Summary = summary,
                    SourceUrl = documentUrl,
                    Publisher = "SEC EDGAR",
                    PublishedAt = filingDate,
                    EventCategory = eventCategory
                };

                // Enrich with source metadata
                EnrichArticleMetadata(article);

                _context.Articles.Add(article);
                articles.Add(article);
            }

            if (articles.Any())
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Ingested {Count} new SEC filings for ticker {Ticker}",
                    articles.Count, ticker);
            }

            return articles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching SEC filings for CIK {CIK}", cik);
            return new List<Article>();
        }
    }

    private (string headline, string summary, EventCategory category) GenerateFilingSummary(string form, string ticker, string primaryDocument)
    {
        return form switch
        {
            "8-K" => ($"{ticker} files 8-K: Material Event Disclosed",
                     "Company filed Form 8-K with the SEC, disclosing a material event. This may include M&A, leadership changes, financial results, or other significant corporate actions.",
                     EventCategory.RegulatoryLegal),

            "10-Q" => ($"{ticker} files 10-Q: Quarterly Financial Report",
                      "Company filed Form 10-Q with the SEC, providing quarterly financial statements and management discussion. Review for revenue, earnings, and forward guidance.",
                      EventCategory.EarningsBeatMiss),

            "10-K" => ($"{ticker} files 10-K: Annual Financial Report",
                      "Company filed Form 10-K with the SEC, providing comprehensive annual financial statements, risk factors, and business overview.",
                      EventCategory.EarningsBeatMiss),

            _ => ($"{ticker} files {form} with SEC",
                 $"Company filed Form {form} with the SEC.",
                 EventCategory.Unknown)
        };
    }

    public override async Task<bool> ValidateConfigurationAsync()
    {
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            // Test with a simple request
            var testUrl = $"{BaseUrl}/files/company_tickers.json";
            var response = await _httpClient.GetAsync(testUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// SEC API response models
public class SecCompanyInfo
{
    public int CikStr { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class SecSubmissionsResponse
{
    public string? Cik { get; set; }
    public string? Name { get; set; }
    public SecFilingsData? Filings { get; set; }
}

public class SecFilingsData
{
    public SecRecentFilings? Recent { get; set; }
}

public class SecRecentFilings
{
    public List<string> AccessionNumber { get; set; } = new();
    public List<string> FilingDate { get; set; } = new();
    public List<string> ReportDate { get; set; } = new();
    public List<string> Form { get; set; } = new();
    public List<string> PrimaryDocument { get; set; } = new();
}
