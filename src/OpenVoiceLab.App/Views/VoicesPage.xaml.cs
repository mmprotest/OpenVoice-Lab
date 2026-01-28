using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab;
using OpenVoiceLab.ViewModels;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace OpenVoiceLab.Views;

public sealed partial class VoicesPage : Page
{
    public VoicesViewModel ViewModel { get; }
    private readonly MediaPlayer _player = new();

    public VoicesPage()
    {
        InitializeComponent();
        ViewModel = new VoicesViewModel(AppServices.Current!);
        DataContext = ViewModel;
        _ = ViewModel.LoadAsync();
        KeepRefAudioToggle.IsChecked = ViewModels.SettingsStore.KeepRefAudio;
    }

    private async void OnRefresh(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void OnPreview(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var path = await ViewModel.PreviewAsync("Hello from OpenVoice Lab.");
        if (!string.IsNullOrWhiteSpace(path))
        {
            _player.Source = MediaSource.CreateFromUri(new Uri(path));
            _player.Play();
        }
    }

    private async void OnRename(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedVoice == null)
        {
            return;
        }
        var dialog = new ContentDialog
        {
            Title = "Rename voice",
            Content = new TextBox { Text = ViewModel.SelectedVoice.Name },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Content is TextBox box)
        {
            await ViewModel.RenameAsync(ViewModel.SelectedVoice.VoiceId, box.Text);
        }
    }

    private async void OnDelete(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedVoice == null)
        {
            return;
        }
        await ViewModel.DeleteAsync(ViewModel.SelectedVoice.VoiceId);
    }

    private async void OnCreateClone(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowHelper.GetWindowHandle());
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".mp3");
        var file = await picker.PickSingleFileAsync();
        if (file == null)
        {
            return;
        }
        await using var stream = await file.OpenStreamForReadAsync();
        var modelSize = (CloneModelSizeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "0.6b";
        var backend = (CloneBackendBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "auto";
        var keepRefAudio = KeepRefAudioToggle.IsChecked == true;
        ViewModels.SettingsStore.KeepRefAudio = keepRefAudio;
        await ViewModel.CreateCloneAsync(
            CloneNameBox.Text,
            modelSize,
            backend,
            keepRefAudio,
            ConsentToggle.IsChecked == true,
            CloneRefTextBox.Text,
            stream,
            file.Name
        );
    }

    private async void OnCreateDesign(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var modelSize = (DesignModelSizeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.7b";
        var backend = (DesignBackendBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "auto";
        await ViewModel.CreateDesignAsync(
            DesignNameBox.Text,
            DesignDescriptionBox.Text,
            DesignSeedTextBox.Text,
            modelSize,
            backend
        );
    }
}
