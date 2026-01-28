using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab;
using OpenVoiceLab.ViewModels;

namespace OpenVoiceLab.Views;

public sealed partial class PronunciationPage : Page
{
    public PronunciationViewModel ViewModel { get; }

    public PronunciationPage()
    {
        InitializeComponent();
        ViewModel = new PronunciationViewModel(AppServices.Current!);
        DataContext = ViewModel;
        _ = ViewModel.LoadAsync();
    }

    private async void OnNewProfile(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var nameBox = new TextBox { PlaceholderText = "Profile name" };
        var dialog = new ContentDialog
        {
            Title = "New pronunciation profile",
            Content = nameBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.CreateProfileAsync(nameBox.Text);
        }
    }

    private async void OnDeleteProfile(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedProfile == null)
        {
            return;
        }
        var dialog = new ContentDialog
        {
            Title = "Delete profile",
            Content = "Are you sure you want to delete this pronunciation profile?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteSelectedAsync();
        }
    }

    private void OnAddEntry(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.AddEntry();
    }

    private void OnRemoveEntry(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PronunciationEntryItem entry)
        {
            ViewModel.RemoveEntry(entry);
        }
    }

    private async void OnSave(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.SaveAsync();
    }
}
