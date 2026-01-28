using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab;
using OpenVoiceLab.ViewModels;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace OpenVoiceLab.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }
    private readonly MediaPlayer _player = new();

    public HistoryPage()
    {
        InitializeComponent();
        ViewModel = new HistoryViewModel(AppServices.Current!);
        DataContext = ViewModel;
        _ = ViewModel.LoadAsync();
    }

    private async void OnSearch(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void OnRefresh(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private void OnPlay(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry == null)
        {
            return;
        }
        _player.Source = MediaSource.CreateFromUri(new Uri(ViewModel.SelectedEntry.OutputPath));
        _player.Play();
    }

    private async void OnExport(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry == null)
        {
            return;
        }
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowHelper.GetWindowHandle());
        picker.FileTypeChoices.Add("WAV", new List<string> { ".wav" });
        picker.SuggestedFileName = "openvoice_history";
        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            File.Copy(ViewModel.SelectedEntry.OutputPath, file.Path, true);
        }
    }
}
