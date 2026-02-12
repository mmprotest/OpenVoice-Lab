using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

    public async Task<ModelsStatusResponse> GetModelsStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<ModelsStatusResponse>("/models/status", _jsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("No models status response");
    }

    public async Task<ModelDownloadResponse> DownloadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/models/download", new ModelDownloadRequest(modelId), _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ModelDownloadResponse>(_jsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("No download response");
    }

    public Task<ModelDownloadResponse> StartModelDownloadAsync(string modelId, CancellationToken cancellationToken = default)
    {
        return DownloadModelAsync(modelId, cancellationToken);
    }

    public async IAsyncEnumerable<ModelDownloadEvent> StreamModelDownloadEventsAsync(string modelId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync(
            $"/models/download/events?model_id={Uri.EscapeDataString(modelId)}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        string? dataBuffer = null;
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                break;
            }
            if (line.Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(dataBuffer))
                {
                    TryYieldEvent(dataBuffer, out var evt);
                    if (evt != null)
                    {
                        yield return evt;
                    }
                }
                dataBuffer = null;
                continue;
            }
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }
            var payload = line[5..].TrimStart();
            dataBuffer = dataBuffer == null ? payload : $"{dataBuffer}\n{payload}";
        }
        if (!string.IsNullOrWhiteSpace(dataBuffer))
        {
            TryYieldEvent(dataBuffer, out var evt);
            if (evt != null)
            {
                yield return evt;
            }
        }
    }

    private bool TryYieldEvent(string payload, out ModelDownloadEvent? evt)
    {
        try
        {
            evt = JsonSerializer.Deserialize<ModelDownloadEvent>(payload, _jsonOptions);
        }
        catch (JsonException)
        {
            evt = null;
        }
        return evt != null;
    }

    public async Task<VoicesResponse> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<VoicesResponse>("/voices", _jsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("No voices response");
    }

    public async Task<VoiceCloneResponse> CreateCloneVoiceAsync(
        string name,
        string modelSize,
        string backend,
        bool keepRefAudio,
        bool consent,
        string? refText,
        Stream audioStream,
        string filename,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(name), "name");
        content.Add(new StringContent(modelSize), "model_size");
        content.Add(new StringContent(backend), "backend");
        content.Add(new StringContent(keepRefAudio.ToString().ToLowerInvariant()), "keep_ref_audio");
        content.Add(new StringContent(consent.ToString().ToLowerInvariant()), "consent");
        if (!string.IsNullOrWhiteSpace(refText))
        {
            content.Add(new StringContent(refText), "ref_text");
        }
        var fileContent = new StreamContent(audioStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "audio", filename);
        var response = await _http.PostAsync("/voices/clone", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VoiceCloneResponse>(_jsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("No clone response");
    }

    public async Task<VoiceDesignResponse> CreateDesignVoiceAsync(VoiceDesignRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/voices/design", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<VoiceDesignResponse>(_jsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("No design response");
    }

    public async Task UpdateVoiceAsync(string voiceId, VoicePatchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PatchAsync($"/voices/{voiceId}", JsonContent.Create(request, options: _jsonOptions), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteVoiceAsync(string voiceId, CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync($"/voices/{voiceId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TtsResponse> CreateTtsAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/tts", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TtsResponse>(_jsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("No TTS response");
    }

    public async Task<Stream> StreamTtsAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var content = JsonContent.Create(request, options: _jsonOptions);
        var message = new HttpRequestMessage(HttpMethod.Post, "/tts/stream")
        {
            Content = content
        };
        var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<ProjectsResponse> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<ProjectsResponse>("/projects", _jsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("No projects response");
    }

    public async Task<ProjectCreateResponse> CreateProjectAsync(ProjectCreateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/projects", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ProjectCreateResponse>(_jsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("No project response");
    }

    public async Task<HistoryResponse> GetHistoryAsync(int limit, string? projectId, string? query, CancellationToken cancellationToken = default)
    {
        var queryString = new StringBuilder($"/history?limit={limit}");
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            queryString.Append("&project_id=").Append(Uri.EscapeDataString(projectId));
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            queryString.Append("&q=").Append(Uri.EscapeDataString(query));
        }
        var response = await _http.GetFromJsonAsync<HistoryResponse>(queryString.ToString(), _jsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("No history response");
    }

    public async Task<HistoryEntry> GetHistoryEntryAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<HistoryEntry>($"/history/{jobId}", _jsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("No history entry");
    }

    public async Task<PronunciationProfilesResponse> GetPronunciationProfilesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<PronunciationProfilesResponse>("/pronunciation/profiles", _jsonOptions, cancellationToken);
        return response ?? throw new InvalidOperationException("No pronunciation response");
    }

    public async Task<PronunciationProfileCreateResponse> CreatePronunciationProfileAsync(PronunciationProfileCreateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("/pronunciation/profiles", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PronunciationProfileCreateResponse>(_jsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("No pronunciation create response");
    }

    public async Task UpdatePronunciationProfileAsync(string profileId, PronunciationProfileUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync($"/pronunciation/profiles/{profileId}", request, _jsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePronunciationProfileAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync($"/pronunciation/profiles/{profileId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DeleteDataResponse> DeleteDataAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.DeleteAsync("/data", cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DeleteDataResponse>(_jsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("No delete response");
    }
}

public record VoiceDesignRequest(string Name, string Description, string SeedText, string ModelSize, string Backend);

public record VoicePatchRequest(string? Name, IReadOnlyList<string>? Tags);
