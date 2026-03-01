using Lease.Domain.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace LeaseParserFunction.Services;

public class HmlrClient
{
    private readonly HttpClient _http;
    private readonly string _username;
    private readonly string _password;

    public HmlrClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _username = configuration["Hmlr:Username"] ?? "";
        _password = configuration["Hmlr:Password"] ?? "";
    }

    public async Task<List<RawScheduleNoticeOfLease>> GetSchedulesAsync()
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "schedules");

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_username}:{_password}"));

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var response = await _http.SendAsync(request);

            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            {
                response.EnsureSuccessStatusCode();
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new HttpRequestException($"Server error {(int)response.StatusCode}");
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<List<RawScheduleNoticeOfLease>>(json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
        });
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
    {
        const int maxRetries = 3;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                lastException = ex;
                Console.WriteLine($"Retry attempt {attempt} due to: {ex.Message}");
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
            }
        }

        throw lastException ?? 
              new Exception("HMLR request failed after maximum retries.");
    }
}