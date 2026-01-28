using Microsoft.UI.Xaml;

namespace OpenVoiceLab;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = AppServices.CreateAndSetCurrent();
        _ = services.InitializeAsync();
        var window = new MainWindow();
        MainWindow = window;
        window.Activate();
    }
}
