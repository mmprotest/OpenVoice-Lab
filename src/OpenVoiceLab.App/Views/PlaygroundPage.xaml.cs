using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab;
using OpenVoiceLab.ViewModels;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace OpenVoiceLab.Views;

public sealed partial class PlaygroundPage : Page
{
    public PlaygroundViewModel ViewModel { get; }
    private readonly MediaPlayer _player = new();

    public PlaygroundPage()
    {
        InitializeComponent();
        ViewModel = new PlaygroundViewModel(AppServices.Current!);
        DataContext = ViewModel;
        _ = ViewModel.LoadAsync();
    }

    private async void OnGenerate(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.GenerateAsync();
        PlayLast();
    }

    private async void OnStream(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await ViewModel.StreamAsync();
        if (path != null)
        {
            PlayLast();
        }
    }

    private void OnPlay(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        PlayLast();
    }

    private async void OnExport(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.LastOutputPath))
        {
            return;
        }
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowHelper.GetWindowHandle());
        picker.FileTypeChoices.Add("WAV", new List<string> { ".wav" });
        picker.SuggestedFileName = "openvoice";
        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            File.Copy(ViewModel.LastOutputPath, file.Path, true);
        }
    }

    private void PlayLast()
    {
        if (string.IsNullOrWhiteSpace(ViewModel.LastOutputPath))
        {
            return;
        }
        _player.Source = MediaSource.CreateFromUri(new Uri(ViewModel.LastOutputPath));
        _player.Play();
    }

    private void OnOpenModels(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        App.MainWindow?.NavigateTo("models");
    }
}
