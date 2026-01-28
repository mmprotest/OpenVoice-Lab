using Microsoft.UI.Xaml;

namespace OpenVoiceLab;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = AppServices.CreateAndSetCurrent();
        _ = services.InitializeAsync();
        var window = new MainWindow();
        window.Activate();
    }
}
