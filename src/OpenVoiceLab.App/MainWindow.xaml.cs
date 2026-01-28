using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenVoiceLab.Views;

namespace OpenVoiceLab;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowHelper.SetWindow(this);
        NavView.SelectionChanged += OnNavigationChanged;
        NavView.SelectedItem = NavView.MenuItems[0];
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
                case "models":
                    ContentFrame.Navigate(typeof(ModelsPage));
                    break;
                case "pronunciation":
                    ContentFrame.Navigate(typeof(PronunciationPage));
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

    public void NavigateTo(string tag)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem && string.Equals(navItem.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                NavView.SelectedItem = navItem;
                break;
            }
        }
    }
}
