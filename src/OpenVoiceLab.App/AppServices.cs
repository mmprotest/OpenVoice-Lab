using System.Net.Http;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab;

public sealed class AppServices
{
    public static AppServices? Current { get; private set; }

    public WorkerSupervisor Worker { get; }

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
        await Worker.WaitForHealthAsync(TimeSpan.FromSeconds(30));
        var http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{Worker.Port}")
        };
        _apiReady.TrySetResult(new ApiClient(http));
    }

    public Task<ApiClient> GetApiAsync() => _apiReady.Task;
}
