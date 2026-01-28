using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab.Views;

namespace OpenVoiceLab;

public sealed partial class MainWindow : Window
{
    private readonly WorkerSupervisor _workerSupervisor = new();

    public MainWindow()
    {
        InitializeComponent();
        NavView.SelectionChanged += OnNavigationChanged;
        NavView.SelectedItem = NavView.MenuItems[0];
        _workerSupervisor.Start();
    }

    private void OnNavigationChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "playground":
                    ContentFrame.Navigate(typeof(PlaygroundPage));
                    break;
                case "voices":
                    ContentFrame.Navigate(typeof(VoicesPage));
                    break;
                case "projects":
                    ContentFrame.Navigate(typeof(ProjectsPage));
                    break;
                case "history":
                    ContentFrame.Navigate(typeof(HistoryPage));
                    break;
                case "settings":
                    ContentFrame.Navigate(typeof(SettingsPage));
                    break;
            }
        }
    }
}
