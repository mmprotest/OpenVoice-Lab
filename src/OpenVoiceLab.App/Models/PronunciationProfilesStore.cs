using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.Models;

public partial class PronunciationProfilesStore : ObservableObject
{
    public ObservableCollection<PronunciationProfile> Profiles { get; } = new();

    public async Task RefreshAsync(ApiClient apiClient, CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetPronunciationProfilesAsync(cancellationToken);
        Profiles.Clear();
        foreach (var profile in response.Profiles)
        {
            Profiles.Add(profile);
        }
    }

    public PronunciationProfile? GetById(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }
        return Profiles.FirstOrDefault(profile => profile.ProfileId == profileId);
    }
}
