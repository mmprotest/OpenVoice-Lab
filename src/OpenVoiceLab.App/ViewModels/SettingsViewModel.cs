using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public SettingsViewModel(AppServices services)
    {
        _services = services;
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
}
