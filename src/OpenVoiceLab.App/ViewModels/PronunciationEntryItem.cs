using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenVoiceLab.ViewModels;

public partial class PronunciationEntryItem : ObservableObject
{
    [ObservableProperty]
    private string _from = string.Empty;

    [ObservableProperty]
    private string _to = string.Empty;
}
