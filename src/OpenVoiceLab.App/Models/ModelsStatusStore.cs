using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.Models;

public partial class ModelsStatusStore : ObservableObject
{
    public ObservableCollection<ModelStatus> Statuses { get; } = new();

    [ObservableProperty]
    private bool _hasCustomVoice;

    [ObservableProperty]
    private bool _hasBase;

    [ObservableProperty]
    private bool _hasVoiceDesign;

    public async Task RefreshAsync(ApiClient apiClient, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetModelsStatusAsync(cancellationToken);
        UpdateStatuses(response.Models);
    }

    public void UpdateStatuses(IReadOnlyList<ModelStatus> models)
    {
        Statuses.Clear();
        foreach (var model in models)
        {
            Statuses.Add(model);
        }
        HasCustomVoice = models.Any(model => model.Kind.Equals("custom_voice", StringComparison.OrdinalIgnoreCase)
            && model.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        HasBase = models.Any(model => model.Kind.Equals("base", StringComparison.OrdinalIgnoreCase)
            && model.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        HasVoiceDesign = models.Any(model => model.Kind.Equals("voice_design", StringComparison.OrdinalIgnoreCase)
            && model.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
    }

    public ModelStatus? GetStatus(string modelId)
    {
        return Statuses.FirstOrDefault(status => status.ModelId == modelId);
    }
}
