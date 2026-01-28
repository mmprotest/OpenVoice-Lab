using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenVoiceLab.Models;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.ViewModels;

public partial class PlaygroundViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<VoiceInfo> Voices { get; } = new();
    public ObservableCollection<string> ModelSizes { get; } = new() { "0.6b", "1.7b" };
    public ObservableCollection<string> Backends { get; } = new() { "auto", "cpu", "cuda" };
    public ObservableCollection<string> Languages { get; } = new() { "Auto", "English", "Chinese" };
    public ObservableCollection<PronunciationProfileOption> PronunciationProfiles { get; } = new();

    [ObservableProperty]
    private VoiceInfo? _selectedVoice;

    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private string? _style;

    [ObservableProperty]
    private string _selectedModelSize = SettingsStore.DefaultModelSize;

    [ObservableProperty]
    private string _selectedBackend = SettingsStore.DefaultBackend;

    [ObservableProperty]
    private string _selectedLanguage = "Auto";

    [ObservableProperty]
    private bool _enableSsmlLite = true;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private string? _lastOutputPath;

    [ObservableProperty]
    private PronunciationProfileOption? _selectedPronunciationProfile;

    [ObservableProperty]
    private bool _isModelsBannerVisible;

    [ObservableProperty]
    private string _modelsBannerMessage = "Models not downloaded. Click here to download.";

    [ObservableProperty]
    private bool _canGenerate = true;

    public PlaygroundViewModel(AppServices services)
    {
        _services = services;
        _services.ModelsStatus.PropertyChanged += (_, _) => UpdateModelAvailability();
        _services.PronunciationProfiles.Profiles.CollectionChanged += (_, _) => RefreshPronunciationProfiles();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var api = await _services.GetApiAsync();
        await _services.ModelsStatus.RefreshAsync(api);
        await _services.PronunciationProfiles.RefreshAsync(api);
        var voices = await api.GetVoicesAsync();
        Voices.Clear();
        foreach (var voice in voices.Voices)
        {
            Voices.Add(voice);
        }
        SelectedVoice = Voices.FirstOrDefault();
        RefreshPronunciationProfiles();
        UpdateModelAvailability();
        Status = "Loaded voices";
    }

    [RelayCommand]
    public async Task GenerateAsync()
    {
        if (!CanGenerate)
        {
            Status = "Download a CustomVoice model to generate.";
            return;
        }
        if (SelectedVoice == null || string.IsNullOrWhiteSpace(Text))
        {
            Status = "Select a voice and enter text.";
            return;
        }
        var api = await _services.GetApiAsync();
        var request = new TtsRequest(
            SelectedVoice.VoiceId,
            Text,
            SelectedLanguage,
            Style,
            SelectedModelSize,
            SelectedBackend,
            24000,
            EnableSsmlLite,
            SelectedPronunciationProfile?.Id ?? SettingsStore.DefaultPronunciationProfileId,
            SettingsStore.CurrentProjectId
        );
        var response = await api.CreateTtsAsync(request);
        LastOutputPath = response.OutputPath;
        Status = $"Generated {response.DurationMs} ms";
    }

    public async Task<string?> StreamAsync()
    {
        if (!CanGenerate)
        {
            Status = "Download a CustomVoice model to stream.";
            return null;
        }
        if (SelectedVoice == null || string.IsNullOrWhiteSpace(Text))
        {
            Status = "Select a voice and enter text.";
            return null;
        }
        var api = await _services.GetApiAsync();
        var request = new TtsRequest(
            SelectedVoice.VoiceId,
            Text,
            SelectedLanguage,
            Style,
            SelectedModelSize,
            SelectedBackend,
            24000,
            EnableSsmlLite,
            SelectedPronunciationProfile?.Id ?? SettingsStore.DefaultPronunciationProfileId,
            SettingsStore.CurrentProjectId
        );
        using var stream = await api.StreamTtsAsync(request);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var bytes = memory.ToArray();
        var sampleRate = 24000;
        LastOutputPath = await WaveFileWriter.WritePcmToTempAsync(bytes, sampleRate);
        Status = "Stream completed";
        return LastOutputPath;
    }

    private void UpdateModelAvailability()
    {
        CanGenerate = _services.ModelsStatus.HasCustomVoice;
        IsModelsBannerVisible = !CanGenerate;
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
        SelectedPronunciationProfile = PronunciationProfiles.FirstOrDefault(option => option.Id == defaultId)
            ?? PronunciationProfiles.FirstOrDefault();
    }
}
