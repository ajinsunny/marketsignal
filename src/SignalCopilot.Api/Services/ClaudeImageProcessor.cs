using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace SignalCopilot.Api.Services;

public class ClaudeImageProcessor : IImageProcessor
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaudeImageProcessor> _logger;

    public ClaudeImageProcessor(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ClaudeImageProcessor> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<string>> ExtractTickersFromImageAsync(byte[] imageData, string contentType)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey == "your_anthropic_api_key_here")
        {
            _logger.LogWarning("Anthropic API key not configured. Cannot extract tickers from image.");
            return new List<string>();
        }

        try
        {
            // Convert image to base64
            var base64Image = Convert.ToBase64String(imageData);

            // Determine media type
            var mediaType = contentType.ToLower() switch
            {
                "image/jpeg" or "image/jpg" => "image/jpeg",
                "image/png" => "image/png",
                "image/gif" => "image/gif",
                "image/webp" => "image/webp",
                _ => "image/jpeg"
            };

            // Create request to Claude API
            var request = new
            {
                model = "claude-3-5-sonnet-20241022",
                max_tokens = 1024,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = mediaType,
                                    data = base64Image
                                }
                            },
                            new
                            {
                                type = "text",
                                text = @"Please extract all stock ticker symbols from this image.
Look for common stock ticker patterns (typically 1-5 uppercase letters like AAPL, MSFT, GOOGL, NVDA, TSLA, etc.).
Return ONLY a JSON array of ticker symbols, nothing else. Format: [""AAPL"", ""MSFT"", ""GOOGL""]
If you see quantities, shares, prices, or other data, ignore them - only extract the ticker symbols."
                            }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Claude API returned {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return new List<string>();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (claudeResponse?.Content == null || !claudeResponse.Content.Any())
            {
                _logger.LogWarning("No content in Claude response");
                return new List<string>();
            }

            var textContent = claudeResponse.Content.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrEmpty(textContent))
            {
                _logger.LogWarning("No text content in Claude response");
                return new List<string>();
            }

            // Try to parse as JSON array
            try
            {
                // Extract JSON array from the response (in case there's extra text)
                var jsonMatch = Regex.Match(textContent, @"\[[\s\S]*?\]");
                if (jsonMatch.Success)
                {
                    var tickers = JsonSerializer.Deserialize<List<string>>(jsonMatch.Value);
                    if (tickers != null && tickers.Any())
                    {
                        // Clean and validate tickers
                        var validTickers = tickers
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .Select(t => t.Trim().ToUpper())
                            .Where(t => Regex.IsMatch(t, @"^[A-Z]{1,5}$"))
                            .Distinct()
                            .ToList();

                        _logger.LogInformation("Extracted {Count} tickers from image: {Tickers}",
                            validTickers.Count, string.Join(", ", validTickers));

                        return validTickers;
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse tickers from Claude response: {Response}", textContent);
            }

            // Fallback: extract ticker-like patterns from text
            var matches = Regex.Matches(textContent, @"\b[A-Z]{1,5}\b");
            var fallbackTickers = matches
                .Select(m => m.Value)
                .Distinct()
                .ToList();

            _logger.LogInformation("Extracted {Count} tickers using fallback method: {Tickers}",
                fallbackTickers.Count, string.Join(", ", fallbackTickers));

            return fallbackTickers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tickers from image");
            return new List<string>();
        }
    }
}

// Response models for Claude API
public class ClaudeResponse
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Role { get; set; }
    public List<ClaudeContent>? Content { get; set; }
    public string? Model { get; set; }
    public string? StopReason { get; set; }
    public ClaudeUsage? Usage { get; set; }
}

public class ClaudeContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

public class ClaudeUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}
