using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.ViewModels;

public partial class VoicesViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<VoiceInfo> Voices { get; } = new();

    [ObservableProperty]
    private VoiceInfo? _selectedVoice;

    [ObservableProperty]
    private string? _status;

    public VoicesViewModel(AppServices services)
    {
        _services = services;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var api = await _services.GetApiAsync();
        var response = await api.GetVoicesAsync();
        Voices.Clear();
        foreach (var voice in response.Voices)
        {
            Voices.Add(voice);
        }
        SelectedVoice = Voices.FirstOrDefault();
        Status = "Loaded voices";
    }

    public async Task<string?> PreviewAsync(string text)
    {
        if (SelectedVoice == null)
        {
            Status = "Select a voice";
            return null;
        }
        var api = await _services.GetApiAsync();
        var request = new TtsRequest(
            SelectedVoice.VoiceId,
            text,
            "Auto",
            null,
            SettingsStore.DefaultModelSize,
            SettingsStore.DefaultBackend,
            24000,
            true,
            null,
            SettingsStore.CurrentProjectId
        );
        var response = await api.CreateTtsAsync(request);
        Status = $"Preview ready: {response.OutputPath}";
        return response.OutputPath;
    }

    public async Task<string?> CreateCloneAsync(
        string name,
        string modelSize,
        string backend,
        bool keepRefAudio,
        bool consent,
        string? refText,
        Stream audioStream,
        string filename)
    {
        var api = await _services.GetApiAsync();
        var response = await api.CreateCloneVoiceAsync(name, modelSize, backend, keepRefAudio, consent, refText, audioStream, filename);
        Status = "Clone voice created";
        await LoadAsync();
        return response.VoiceId;
    }

    public async Task<string?> CreateDesignAsync(string name, string description, string seedText, string modelSize, string backend)
    {
        var api = await _services.GetApiAsync();
        var response = await api.CreateDesignVoiceAsync(new VoiceDesignRequest(name, description, seedText, modelSize, backend));
        Status = "Design voice created";
        await LoadAsync();
        return response.VoiceId;
    }

    public async Task RenameAsync(string voiceId, string newName)
    {
        var api = await _services.GetApiAsync();
        await api.UpdateVoiceAsync(voiceId, new VoicePatchRequest(newName, null));
        Status = "Voice renamed";
        await LoadAsync();
    }

    public async Task DeleteAsync(string voiceId)
    {
        var api = await _services.GetApiAsync();
        await api.DeleteVoiceAsync(voiceId);
        Status = "Voice deleted";
        await LoadAsync();
    }
}
