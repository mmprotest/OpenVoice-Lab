using System.Diagnostics;
using System.Net;
using System.Net.Http;

namespace OpenVoiceLab;

public class WorkerSupervisor
{
    private Process? _process;
    private int _port;

    public int Port => _port;

    public void Start()
    {
        _port = FindFreePort();
        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"-m uvicorn app:app --host 127.0.0.1 --port {_port}",
            WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "worker"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.Exited += (_, _) => Restart();
        _process.Start();
    }

    public string BuildHealthUrl() => $"http://127.0.0.1:{_port}/health";

    public async Task<bool> WaitForHealthAsync(TimeSpan timeout)
    {
        using var http = new HttpClient();
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var response = await http.GetAsync(BuildHealthUrl());
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
            }
            catch
            {
            }
            await Task.Delay(500);
        }
        return false;
    }

    private void Restart()
    {
        _process?.Dispose();
        Start();
    }

    public int FindFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
