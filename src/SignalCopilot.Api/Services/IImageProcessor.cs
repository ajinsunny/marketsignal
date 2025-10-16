namespace SignalCopilot.Api.Services;

public interface IImageProcessor
{
    Task<List<string>> ExtractTickersFromImageAsync(byte[] imageData, string contentType);
}
