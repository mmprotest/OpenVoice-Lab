using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab;
using OpenVoiceLab.ViewModels;

namespace OpenVoiceLab.Views;

public sealed partial class ProjectsPage : Page
{
    public ProjectsViewModel ViewModel { get; }

    public ProjectsPage()
    {
        InitializeComponent();
        ViewModel = new ProjectsViewModel(AppServices.Current!);
        DataContext = ViewModel;
        _ = ViewModel.LoadAsync();
    }

    private async void OnCreate(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectNameBox.Text))
        {
            return;
        }
        await ViewModel.CreateAsync(ProjectNameBox.Text);
        ProjectNameBox.Text = string.Empty;
    }

    private async void OnRefresh(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private void OnSelect(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.SelectedProject != null)
        {
            ViewModel.SelectProject(ViewModel.SelectedProject);
        }
    }
}
