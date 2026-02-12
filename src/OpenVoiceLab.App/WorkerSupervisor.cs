using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace OpenVoiceLab;

public class WorkerSupervisor
{
    private Process? _process;
    private int _port;
    private bool _stopping;
    private readonly object _lock = new();
    private readonly string _logDir;
    private readonly string _logPath;
    private readonly string _workerDir;
    private TaskCompletionSource<int> _portTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Port => _port;
    public string LogPath => _logPath;
    public string LogDirectory => _logDir;

    public WorkerSupervisor()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDir = Path.Combine(localAppData, "OpenVoiceLab", "logs");
        Directory.CreateDirectory(_logDir);
        _logPath = Path.Combine(_logDir, "worker.log");
        _workerDir = LocateWorkerDirectory();
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_stopping)
            {
                _stopping = false;
            }
            _port = 0;
            _portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var workerExePath = LocateWorkerExecutable(_workerDir);
            if (string.IsNullOrWhiteSpace(workerExePath))
            {
                AppendLog("Worker executable not found. Ensure the worker runtime is installed.");
                _portTcs.TrySetException(new InvalidOperationException("Worker executable not found."));
                return;
            }
            var startInfo = new ProcessStartInfo
            {
                FileName = workerExePath,
                Arguments = "--host 127.0.0.1 --port 0 --log-level info",
                WorkingDirectory = _workerDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cachePath = Path.Combine(localAppData, "OpenVoiceLab", "hf");
            startInfo.Environment["HF_HOME"] = cachePath;
            startInfo.Environment["TRANSFORMERS_CACHE"] = cachePath;
            startInfo.Environment["HF_HUB_DISABLE_TELEMETRY"] = "1";
            startInfo.Environment["TOKENIZERS_PARALLELISM"] = "false";
            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, args) => HandleOutput(args.Data);
            _process.ErrorDataReceived += (_, args) => AppendLog(args.Data);
            _process.Exited += OnProcessExited;
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
    }

    public string BuildHealthUrl() => $"http://127.0.0.1:{_port}/health";

    public async Task<int> WaitForPortAsync(TimeSpan timeout)
    {
        var completed = await Task.WhenAny(_portTcs.Task, Task.Delay(timeout));
        if (completed != _portTcs.Task)
        {
            throw new TimeoutException("Timed out waiting for worker port.");
        }
        return await _portTcs.Task;
    }

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

    public void AppendLogEntry(string message)
    {
        AppendLog(message);
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

    private void HandleOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }
        AppendLog(line);
        if (TryParseWorkerPort(line, out var port))
        {
            _port = port;
            AppendLog($"Worker port detected: {port}");
            _portTcs.TrySetResult(port);
        }
    }

    private void Restart()
    {
        if (_stopping)
        {
            return;
        }
        _process?.Dispose();
        Task.Delay(1000).ContinueWith(_ => Start());
    }

    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public async Task StopAsync()
    {
        lock (_lock)
        {
            _stopping = true;
            if (_process is null)
            {
                return;
            }
            _process.Exited -= OnProcessExited;
        }
        await RequestShutdownAsync();
        try
        {
            if (_process is null)
            {
                return;
            }
            if (!_process.HasExited)
            {
                await Task.Run(() => _process.WaitForExit(5000));
            }
            if (!_process.HasExited)
            {
                _process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    private async Task RequestShutdownAsync()
    {
        if (_port <= 0)
        {
            return;
        }
        try
        {
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };
            await http.PostAsync($"http://127.0.0.1:{_port}/shutdown", new StringContent(string.Empty));
        }
        catch
        {
        }
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

    private static string? LocateWorkerExecutable(string workerDir)
    {
        var primary = Path.Combine(workerDir, "OpenVoiceLab.Worker.exe");
        if (File.Exists(primary))
        {
            return primary;
        }
        var fallback = Path.Combine(workerDir, "dist", "OpenVoiceLab.Worker", "OpenVoiceLab.Worker.exe");
        if (File.Exists(fallback))
        {
            return fallback;
        }
        return null;
    }

    internal static bool TryParseWorkerPort(string line, out int port)
    {
        port = 0;
        const string Prefix = "WORKER_PORT=";
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }
        return int.TryParse(line[Prefix.Length..], out port);
    }

    private void OnProcessExited(object? sender, EventArgs args)
    {
        if (!_portTcs.Task.IsCompleted)
        {
            _portTcs.TrySetException(new InvalidOperationException("Worker exited before reporting port."));
        }
        Restart();
    }
}
