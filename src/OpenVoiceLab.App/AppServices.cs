using System.Net.Http;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab;

public sealed class AppServices
{
    public static AppServices? Current { get; private set; }

    public WorkerSupervisor Worker { get; }

    public AppState State { get; } = new();

    public Models.ModelsStatusStore ModelsStatus { get; } = new();

    public Models.PronunciationProfilesStore PronunciationProfiles { get; } = new();

    private readonly TaskCompletionSource<ApiClient> _apiReady = new();

    public AppServices()
    {
        Worker = new WorkerSupervisor();
    }

    public static AppServices CreateAndSetCurrent()
    {
        var services = new AppServices();
        Current = services;
        return services;
    }

    public async Task InitializeAsync()
    {
        Worker.Start();
        try
        {
            var port = await Worker.WaitForPortAsync(TimeSpan.FromSeconds(30));
            var http = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}")
            };
            if (!await Worker.WaitForHealthAsync(TimeSpan.FromSeconds(45)))
            {
                await HandleWorkerStartupFailureAsync();
                return;
            }
            State.ClearWorkerError();
            var apiClient = new ApiClient(http);
            _apiReady.TrySetResult(apiClient);
            await Task.WhenAll(
                ModelsStatus.RefreshAsync(apiClient),
                PronunciationProfiles.RefreshAsync(apiClient));
        }
        catch (TimeoutException)
        {
            await HandleWorkerStartupFailureAsync();
        }
        catch (InvalidOperationException)
        {
            await HandleWorkerStartupFailureAsync();
        }
    }

    public Task<ApiClient> GetApiAsync() => _apiReady.Task;

    private async Task HandleWorkerStartupFailureAsync()
    {
        State.SetWorkerError(new WorkerErrorState(
            "Worker failed to start",
            "The local voice engine did not become ready. Open logs.",
            Worker.LogDirectory));
        Worker.AppendLogEntry("Worker failed to start: health check timeout.");
        _apiReady.TrySetException(new InvalidOperationException("Worker failed to start."));
        await Worker.StopAsync();
    }
}
