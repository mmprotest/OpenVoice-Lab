namespace OpenVoiceLab;

public sealed class AppState
{
    public WorkerErrorState? WorkerError { get; private set; }

    internal void SetWorkerError(WorkerErrorState error)
    {
        WorkerError = error;
    }

    internal void ClearWorkerError()
    {
        WorkerError = null;
    }
}

public sealed record WorkerErrorState(string Title, string Message, string LogPath);
