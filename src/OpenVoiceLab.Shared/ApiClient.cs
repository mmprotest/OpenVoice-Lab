using System.Net.Http.Json;
using System.Text.Json;

namespace OpenVoiceLab.Shared;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient)
    {
        _http = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<HealthResponse>("/health", _jsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("No health response");
    }

    public async Task<SystemInfo> GetSystemAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<SystemInfo>("/system", _jsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("No system response");
    }

    public async Task<TtsResponse> CreateTtsAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/tts", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TtsResponse>(_jsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("No TTS response");
    }
}
