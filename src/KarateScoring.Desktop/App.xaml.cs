using System.Windows;
using KarateScoring.Desktop.Services;

namespace KarateScoring.Desktop;

public partial class App : Application
{
    public ApiHostLauncher ApiHost { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var splash = new SplashWindow();
        splash.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ApiHost.Dispose();
        base.OnExit(e);
    }
}
