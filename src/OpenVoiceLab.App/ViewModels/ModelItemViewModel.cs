using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.ViewModels;

public partial class ModelItemViewModel : ObservableObject
{
    public string ModelId { get; }
    public string Kind { get; }
    public string Size { get; }

    [ObservableProperty]
    private string _status = "unknown";

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private long _downloadedBytes;

    [ObservableProperty]
    private long _totalBytes;

    [ObservableProperty]
    private string? _path;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isDownloaded;

    [ObservableProperty]
    private bool _isPathAvailable;

    public ModelItemViewModel(ModelSupport support)
    {
        ModelId = support.ModelId;
        Kind = support.Kind;
        Size = support.Size;
    }

    public string DisplayName => $"{KindDisplay} {Size}";

    public string KindDisplay => Kind switch
    {
        "custom_voice" => "CustomVoice",
        "base" => "Base",
        "voice_design" => "VoiceDesign",
        _ => Kind
    };

    public bool CanDownload => !IsDownloaded && !IsDownloading;

    public bool ShowProgress => IsDownloading || (Progress > 0 && !IsDownloaded);

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public string ProgressText => $"{Progress}%";

    public bool CanOpenFolder => IsPathAvailable;

    public void UpdateFromStatus(ModelStatus status)
    {
        Status = status.Status;
        Progress = status.Progress;
        DownloadedBytes = status.DownloadedBytes;
        TotalBytes = status.TotalBytes;
        Path = status.Path;
        Error = status.Error;
        IsDownloaded = status.Status.Equals("completed", StringComparison.OrdinalIgnoreCase);
        IsDownloading = status.Status.Equals("downloading", StringComparison.OrdinalIgnoreCase)
            || status.Status.Equals("pending", StringComparison.OrdinalIgnoreCase);
        UpdatePathAvailability();
    }

    public void UpdateFromEvent(ModelDownloadEvent evt)
    {
        Progress = evt.Pct;
        DownloadedBytes = evt.DownloadedBytes;
        TotalBytes = evt.TotalBytes;
        Error = evt.Error;
        Status = evt.Stage;
        IsDownloaded = evt.Stage.Equals("completed", StringComparison.OrdinalIgnoreCase);
        IsDownloading = evt.Stage.Equals("downloading", StringComparison.OrdinalIgnoreCase)
            || evt.Stage.Equals("pending", StringComparison.OrdinalIgnoreCase);
        UpdatePathAvailability();
    }

    partial void OnIsDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(ShowProgress));
    }

    partial void OnIsDownloadedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(ShowProgress));
    }

    partial void OnProgressChanged(int value)
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ShowProgress));
    }

    partial void OnErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnPathChanged(string? value)
    {
        UpdatePathAvailability();
    }

    private void UpdatePathAvailability()
    {
        IsPathAvailable = !string.IsNullOrWhiteSpace(Path) && Directory.Exists(Path);
        OnPropertyChanged(nameof(CanOpenFolder));
    }
}
