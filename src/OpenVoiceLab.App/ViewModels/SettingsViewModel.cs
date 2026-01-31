using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using OpenVoiceLab;
using OpenVoiceLab.Models;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;

    [ObservableProperty]
    private string _defaultBackend = SettingsStore.DefaultBackend;

    [ObservableProperty]
    private string _defaultModelSize = SettingsStore.DefaultModelSize;

    [ObservableProperty]
    private bool _keepRefAudio = SettingsStore.KeepRefAudio;

    public ObservableCollection<PronunciationProfileOption> PronunciationProfiles { get; } = new();

    [ObservableProperty]
    private string? _selectedPronunciationProfileId = SettingsStore.DefaultPronunciationProfileId;

    [ObservableProperty]
    private string? _engineLog;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private string? _workerErrorTitle;

    [ObservableProperty]
    private string? _workerErrorMessage;

    [ObservableProperty]
    private string? _workerErrorLogPath;

    [ObservableProperty]
    private bool _hasWorkerError;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        UpdateWorkerError(_services.State.WorkerError);
        RefreshLog();
        _services.PronunciationProfiles.Profiles.CollectionChanged += (_, _) => RefreshPronunciationProfiles();
        RefreshPronunciationProfiles();
        _ = LoadPronunciationProfilesAsync();
    }

    partial void OnDefaultBackendChanged(string value)
    {
        SettingsStore.DefaultBackend = value;
    }

    partial void OnDefaultModelSizeChanged(string value)
    {
        SettingsStore.DefaultModelSize = value;
    }

    partial void OnKeepRefAudioChanged(bool value)
    {
        SettingsStore.KeepRefAudio = value;
    }

    partial void OnSelectedPronunciationProfileIdChanged(string? value)
    {
        SettingsStore.DefaultPronunciationProfileId = value;
    }

    public Visibility WorkerErrorVisibility => HasWorkerError ? Visibility.Visible : Visibility.Collapsed;

    public void RefreshLog()
    {
        EngineLog = _services.Worker.GetLogTail();
    }

    public void OpenLogDirectory()
    {
        var logDir = _services.Worker.LogDirectory;
        Directory.CreateDirectory(logDir);
        Process.Start(new ProcessStartInfo
        {
            FileName = logDir,
            UseShellExecute = true
        });
    }

    public async Task DeleteAllDataAsync()
    {
        var api = await _services.GetApiAsync();
        await api.DeleteDataAsync();
        Status = "Local data deleted";
    }

    private async Task LoadPronunciationProfilesAsync()
    {
        var api = await _services.GetApiAsync();
        await _services.PronunciationProfiles.RefreshAsync(api);
        RefreshPronunciationProfiles();
    }

    private void RefreshPronunciationProfiles()
    {
        PronunciationProfiles.Clear();
        PronunciationProfiles.Add(new PronunciationProfileOption(null, "None"));
        foreach (var profile in _services.PronunciationProfiles.Profiles)
        {
            PronunciationProfiles.Add(new PronunciationProfileOption(profile.ProfileId, profile.Name));
        }
        var defaultId = SettingsStore.DefaultPronunciationProfileId;
        var selected = PronunciationProfiles.FirstOrDefault(option => option.Id == defaultId)
            ?? PronunciationProfiles.FirstOrDefault();
        SelectedPronunciationProfileId = selected?.Id;
    }

    private void UpdateWorkerError(WorkerErrorState? error)
    {
        HasWorkerError = error is not null;
        WorkerErrorTitle = error?.Title;
        WorkerErrorMessage = error?.Message;
        WorkerErrorLogPath = error?.LogPath;
    }

    partial void OnHasWorkerErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(WorkerErrorVisibility));
    }
}
