using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab;
using OpenVoiceLab.ViewModels;

namespace OpenVoiceLab.Views;

public sealed partial class ModelsPage : Page
{
    public ModelsViewModel ViewModel { get; }

    public ModelsPage()
    {
        InitializeComponent();
        ViewModel = new ModelsViewModel(AppServices.Current!);
        DataContext = ViewModel;
        _ = ViewModel.LoadAsync();
    }

    private async void OnRefresh(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void OnDownloadRequired(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.DownloadRequiredAsync();
    }

    private async void OnDownloadModel(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ModelItemViewModel model)
        {
            await ViewModel.DownloadAsync(model);
        }
    }

    private async void OnOpenFolder(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ModelItemViewModel model)
        {
            await ViewModel.OpenModelsFolderAsync(model);
        }
    }
}
