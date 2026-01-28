using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    private string? _engineLog;

    [ObservableProperty]
    private string? _status;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        RefreshLog();
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

    public void RefreshLog()
    {
        EngineLog = _services.Worker.GetLogTail();
    }

    public async Task DeleteAllDataAsync()
    {
        var api = await _services.GetApiAsync();
        await api.DeleteDataAsync();
        Status = "Local data deleted";
    }
}
