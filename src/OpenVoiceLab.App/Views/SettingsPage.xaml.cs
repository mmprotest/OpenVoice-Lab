using System.Linq;
using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab.ViewModels;

namespace OpenVoiceLab.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(AppServices.Current!);
        DataContext = ViewModel;
        BackendBox.SelectedItem = BackendBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content?.ToString() == ViewModel.DefaultBackend);
        ModelSizeBox.SelectedItem = ModelSizeBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content?.ToString() == ViewModel.DefaultModelSize);
        BackendBox.SelectionChanged += OnBackendChanged;
        ModelSizeBox.SelectionChanged += OnModelSizeChanged;
    }

    private void OnBackendChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BackendBox.SelectedItem is ComboBoxItem item && item.Content is string value)
        {
            ViewModel.DefaultBackend = value;
        }
    }

    private void OnModelSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSizeBox.SelectedItem is ComboBoxItem item && item.Content is string value)
        {
            ViewModel.DefaultModelSize = value;
        }
    }

    private async void OnDeleteData(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.DeleteAllDataAsync();
    }

    private void OnRefreshLog(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.RefreshLog();
    }
}
