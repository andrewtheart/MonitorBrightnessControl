using Microsoft.UI.Xaml;

namespace MonitorBrightness;

public partial class App : Application
{
    private Window? _window;

    /// <summary>CLI override for window position (null = use settings).</summary>
    public static WindowPosition? OverridePosition { get; set; }

    /// <summary>CLI override for display number (null = use settings).</summary>
    public static int? OverrideDisplay { get; set; }

    /// <summary>When set, the app uses these simulated monitors instead of real DDC/CI enumeration.</summary>
    public static List<MonitorDevice>? DemoMonitors { get; set; }

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private async void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        AppLogger.Warn("Unhandled exception", e.Exception);

        try
        {
            var xamlRoot = _window?.Content?.XamlRoot;
            if (xamlRoot is not null)
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Unexpected Error",
                    Content = $"{e.Exception.GetType().Name}: {e.Message}\n\nThe application will now close.",
                    CloseButtonText = "Close",
                    XamlRoot = xamlRoot,
                };
                await dialog.ShowAsync();
            }
        }
        catch
        {
            // Dialog failed — close anyway
        }

        Environment.Exit(1);
    }
}
