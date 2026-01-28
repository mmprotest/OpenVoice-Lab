using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace OpenVoiceLab;

public class WorkerSupervisor
{
    private Process? _process;
    private int _port;
    private readonly object _lock = new();
    private readonly string _logPath;

    public int Port => _port;
    public string LogPath => _logPath;

    public WorkerSupervisor()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDir = Path.Combine(localAppData, "OpenVoiceLab", "logs");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, "worker.log");
    }

    public void Start()
    {
        lock (_lock)
        {
            _port = FindFreePort(20000, 40000);
            var workerDir = LocateWorkerDirectory();
            var python = LocatePythonExecutable(workerDir);
            var startInfo = new ProcessStartInfo
            {
                FileName = python,
                Arguments = $"-m uvicorn app:app --host 127.0.0.1 --port {_port}",
                WorkingDirectory = workerDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, args) => AppendLog(args.Data);
            _process.ErrorDataReceived += (_, args) => AppendLog(args.Data);
            _process.Exited += (_, _) => Restart();
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
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

    public string GetLogTail(int maxLines = 200)
    {
        if (!File.Exists(_logPath))
        {
            return string.Empty;
        }
        var lines = File.ReadAllLines(_logPath);
        return string.Join(Environment.NewLine, lines.TakeLast(maxLines));
    }

    private void AppendLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }
        var entry = $"{DateTime.Now:O} {line}{Environment.NewLine}";
        File.AppendAllText(_logPath, entry, Encoding.UTF8);
    }

    private void Restart()
    {
        _process?.Dispose();
        Task.Delay(1000).ContinueWith(_ => Start());
    }

    public int FindFreePort(int minPort, int maxPort)
    {
        var random = new Random();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var port = random.Next(minPort, maxPort + 1);
            try
            {
                var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch
            {
            }
        }
        var fallback = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        fallback.Start();
        var result = ((IPEndPoint)fallback.LocalEndpoint).Port;
        fallback.Stop();
        return result;
    }

    private static string LocateWorkerDirectory()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, "worker");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        return Path.Combine(AppContext.BaseDirectory, "worker");
    }

    private static string LocatePythonExecutable(string workerDir)
    {
        var venvPython = Path.Combine(workerDir, ".venv", "Scripts", "python.exe");
        return File.Exists(venvPython) ? venvPython : "python";
    }
}
