namespace LeaseApi.Services;

public class LeaseProcessingTrigger
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public LeaseProcessingTrigger(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task TriggerAsync(string titleNumber, string? correlationId, CancellationToken ct)
    {
        var baseUrl = _config["Processing:FunctionUrl"]
            ?? throw new InvalidOperationException("Processing:FunctionUrl not configured.");

        var url = $"{baseUrl}?titleNumber={Uri.EscapeDataString(titleNumber)}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);

        if (!string.IsNullOrWhiteSpace(correlationId))
            req.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        using var resp = await _http.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Function trigger failed with status {(int)resp.StatusCode}");
        }
    }
}