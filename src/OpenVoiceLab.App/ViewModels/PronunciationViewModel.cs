using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.ViewModels;

public partial class PronunciationViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<PronunciationProfile> Profiles => _services.PronunciationProfiles.Profiles;

    public ObservableCollection<PronunciationEntryItem> Entries { get; } = new();

    [ObservableProperty]
    private PronunciationProfile? _selectedProfile;

    [ObservableProperty]
    private string _testInput = string.Empty;

    [ObservableProperty]
    private string _previewOutput = string.Empty;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private string? _validationMessage;

    public PronunciationViewModel(AppServices services)
    {
        _services = services;
        Entries.CollectionChanged += OnEntriesChanged;
    }

    public async Task LoadAsync()
    {
        var api = await _services.GetApiAsync();
        await _services.PronunciationProfiles.RefreshAsync(api);
        SelectedProfile = Profiles.FirstOrDefault();
        Status = "Loaded pronunciation profiles";
    }

    public async Task CreateProfileAsync(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ValidationMessage = "Profile name is required.";
            return;
        }
        var api = await _services.GetApiAsync();
        var response = await api.CreatePronunciationProfileAsync(new PronunciationProfileCreateRequest(trimmed));
        await _services.PronunciationProfiles.RefreshAsync(api);
        SelectedProfile = Profiles.FirstOrDefault(profile => profile.ProfileId == response.Id);
        Status = "Profile created";
    }

    public async Task SaveAsync()
    {
        ValidationMessage = null;
        if (SelectedProfile == null)
        {
            ValidationMessage = "Select a profile to save.";
            return;
        }
        var sanitized = new List<PronunciationEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries)
        {
            var from = entry.From.Trim();
            var to = entry.To.Trim();
            if (string.IsNullOrWhiteSpace(from))
            {
                ValidationMessage = "From entries cannot be empty.";
                return;
            }
            if (!seen.Add(from))
            {
                ValidationMessage = "Duplicate 'From' values detected.";
                return;
            }
            sanitized.Add(new PronunciationEntry(from, to));
        }
        for (var idx = 0; idx < Entries.Count; idx++)
        {
            Entries[idx].From = sanitized[idx].From;
            Entries[idx].To = sanitized[idx].To;
        }
        var api = await _services.GetApiAsync();
        await api.UpdatePronunciationProfileAsync(SelectedProfile.ProfileId, new PronunciationProfileUpdateRequest(sanitized));
        await _services.PronunciationProfiles.RefreshAsync(api);
        SelectedProfile = Profiles.FirstOrDefault(profile => profile.ProfileId == SelectedProfile.ProfileId);
        Status = "Profile saved";
        UpdatePreview();
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }
        var api = await _services.GetApiAsync();
        await api.DeletePronunciationProfileAsync(SelectedProfile.ProfileId);
        await _services.PronunciationProfiles.RefreshAsync(api);
        SelectedProfile = Profiles.FirstOrDefault();
        Status = "Profile deleted";
    }

    public void AddEntry()
    {
        Entries.Add(new PronunciationEntryItem());
    }

    public void RemoveEntry(PronunciationEntryItem entry)
    {
        Entries.Remove(entry);
    }

    partial void OnSelectedProfileChanged(PronunciationProfile? value)
    {
        DetachEntryHandlers();
        Entries.Clear();
        if (value != null)
        {
            foreach (var entry in value.Entries)
            {
                var item = new PronunciationEntryItem { From = entry.From, To = entry.To };
                Entries.Add(item);
            }
        }
        UpdatePreview();
    }

    partial void OnTestInputChanged(string value)
    {
        UpdatePreview();
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (PronunciationEntryItem item in e.NewItems)
            {
                item.PropertyChanged += OnEntryPropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (PronunciationEntryItem item in e.OldItems)
            {
                item.PropertyChanged -= OnEntryPropertyChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            DetachEntryHandlers();
        }
        UpdatePreview();
    }

    private void OnEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        PreviewOutput = ApplyPronunciation(TestInput, Entries);
    }

    private void DetachEntryHandlers()
    {
        foreach (var entry in Entries)
        {
            entry.PropertyChanged -= OnEntryPropertyChanged;
        }
    }

    private static string ApplyPronunciation(string text, IEnumerable<PronunciationEntryItem> entries)
    {
        var output = text;
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.From))
            {
                continue;
            }
            var pattern = $@"\b{Regex.Escape(entry.From.Trim())}\b";
            output = Regex.Replace(output, pattern, entry.To.Trim(), RegexOptions.IgnoreCase);
        }
        return output;
    }
}
