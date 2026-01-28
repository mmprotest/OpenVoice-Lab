using System.Collections.ObjectModel;
using System.Linq;
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

    [ObservableProperty]
    private bool _canClone = true;

    [ObservableProperty]
    private bool _canDesign = true;

    [ObservableProperty]
    private bool _isModelsBannerVisible;

    [ObservableProperty]
    private string _modelsBannerMessage = "Models not downloaded. Click here to download.";

    [ObservableProperty]
    private bool _isCloneWarningVisible;

    [ObservableProperty]
    private bool _isDesignWarningVisible;

    public VoicesViewModel(AppServices services)
    {
        _services = services;
        _services.ModelsStatus.PropertyChanged += (_, _) => UpdateModelAvailability();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var api = await _services.GetApiAsync();
        await _services.ModelsStatus.RefreshAsync(api);
        var response = await api.GetVoicesAsync();
        Voices.Clear();
        foreach (var voice in response.Voices)
        {
            Voices.Add(voice);
        }
        SelectedVoice = Voices.FirstOrDefault();
        UpdateModelAvailability();
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
            SettingsStore.DefaultPronunciationProfileId,
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
        if (!CanClone)
        {
            Status = "Download a Base model to clone voices.";
            return null;
        }
        var api = await _services.GetApiAsync();
        var response = await api.CreateCloneVoiceAsync(name, modelSize, backend, keepRefAudio, consent, refText, audioStream, filename);
        Status = "Clone voice created";
        await LoadAsync();
        return response.VoiceId;
    }

    public async Task<string?> CreateDesignAsync(string name, string description, string seedText, string modelSize, string backend)
    {
        if (!CanDesign)
        {
            Status = "Download a VoiceDesign model to design voices.";
            return null;
        }
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

    private void UpdateModelAvailability()
    {
        CanClone = _services.ModelsStatus.HasBase;
        CanDesign = _services.ModelsStatus.HasVoiceDesign;
        IsModelsBannerVisible = !_services.ModelsStatus.HasCustomVoice;
        IsCloneWarningVisible = !CanClone;
        IsDesignWarningVisible = !CanDesign;
    }
}
