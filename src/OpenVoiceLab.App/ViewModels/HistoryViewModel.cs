using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    [ObservableProperty]
    private HistoryEntry? _selectedEntry;

    [ObservableProperty]
    private string? _searchQuery;

    [ObservableProperty]
    private string? _status;

    public HistoryViewModel(AppServices services)
    {
        _services = services;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var api = await _services.GetApiAsync();
        var response = await api.GetHistoryAsync(50, SettingsStore.CurrentProjectId, SearchQuery);
        Entries.Clear();
        foreach (var entry in response.History)
        {
            Entries.Add(entry);
        }
        SelectedEntry = Entries.FirstOrDefault();
        Status = "Loaded history";
    }

    public async Task RefreshAsync()
    {
        await LoadAsync();
    }
}
