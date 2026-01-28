using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.ViewModels;

public partial class PlaygroundViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<VoiceInfo> Voices { get; } = new();
    public ObservableCollection<string> ModelSizes { get; } = new() { "0.6b", "1.7b" };
    public ObservableCollection<string> Backends { get; } = new() { "auto", "cpu", "cuda" };
    public ObservableCollection<string> Languages { get; } = new() { "Auto", "English", "Chinese" };

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

    public PlaygroundViewModel(AppServices services)
    {
        _services = services;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var api = await _services.GetApiAsync();
        var voices = await api.GetVoicesAsync();
        Voices.Clear();
        foreach (var voice in voices.Voices)
        {
            Voices.Add(voice);
        }
        SelectedVoice = Voices.FirstOrDefault();
        Status = "Loaded voices";
    }

    [RelayCommand]
    public async Task GenerateAsync()
    {
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
            null,
            SettingsStore.CurrentProjectId
        );
        var response = await api.CreateTtsAsync(request);
        LastOutputPath = response.OutputPath;
        Status = $"Generated {response.DurationMs} ms";
    }

    public async Task<string?> StreamAsync()
    {
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
            null,
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
}
