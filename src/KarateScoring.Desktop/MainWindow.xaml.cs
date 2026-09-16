using System.Windows;

namespace KarateScoring.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(string baseUrl)
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.Navigate(baseUrl);
        };
    }
}
