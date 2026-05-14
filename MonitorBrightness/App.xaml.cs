using Microsoft.UI.Xaml;

namespace MonitorBrightness;

public partial class App : Application
{
    private Window? _window;

    /// <summary>CLI override for window position (null = use settings).</summary>
    public static WindowPosition? OverridePosition { get; set; }

    /// <summary>CLI override for display number (null = use settings).</summary>
    public static int? OverrideDisplay { get; set; }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
