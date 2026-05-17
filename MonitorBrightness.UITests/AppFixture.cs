using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace MonitorBrightness.UITests;

/// <summary>
/// Launches the app once per test collection.
/// Ensures a clean settings file so the first-launch dialog does not appear.
/// </summary>
public sealed class AppFixture : IDisposable
{
    private static readonly string AppDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "MonitorBrightness", "bin", "Debug", "net10.0-windows10.0.26100.0", "win-x64"));

    private static readonly string ExePath = Path.Combine(AppDir, "MonitorBrightness.exe");
    private static readonly string SettingsPath = Path.Combine(AppDir, "monitor_brightness_settings.json");

    /// <summary>Number of simulated monitors to show (passed as --demo N).</summary>
    public int DemoMonitorCount { get; set; } = 3;

    private string? _originalSettings;
    public Application? App { get; private set; }
    public UIA3Automation Automation { get; } = new();

    public void Launch()
    {
        if (App is not null)
            return;

        BackupSettings();
        WriteTestSettings();

        if (!File.Exists(ExePath))
            throw new FileNotFoundException($"App not found. Build the Debug configuration first: {ExePath}");

        App = Application.Launch(ExePath, $"--demo {DemoMonitorCount}");
    }

    public Window GetMainWindow(int timeoutSeconds = 15)
    {
        if (App is null)
            throw new InvalidOperationException("App not launched. Call Launch() first.");

        return App.GetMainWindow(Automation, TimeSpan.FromSeconds(timeoutSeconds))
            ?? throw new TimeoutException("Main window did not appear.");
    }

    /// <summary>
    /// Wait a short time for UI updates to propagate.
    /// </summary>
    public static void Wait(int ms = 400) => Thread.Sleep(ms);

    private void BackupSettings()
    {
        if (File.Exists(SettingsPath))
            _originalSettings = File.ReadAllText(SettingsPath);
    }

    private static void WriteTestSettings()
    {
        var settings = new Dictionary<string, object>
        {
            ["FirstLaunchHotkeyDialogShown"] = true,
            ["CloseToTray"] = false,   // false so Close() actually exits the process
            ["HotkeyModifiers"] = 0,
            ["HotkeyVirtualKey"] = 0,
            ["HotkeyDisplayText"] = "",
            ["MaxVisibleMonitors"] = 4,
            ["StartPosition"] = "Center",
            ["StartDisplay"] = 0,
        };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
    }

    private void RestoreSettings()
    {
        try
        {
            if (_originalSettings is not null)
                File.WriteAllText(SettingsPath, _originalSettings);
        }
        catch { /* best effort */ }
    }

    public void Dispose()
    {
        try
        {
            App?.Close();
            // Give the process a moment, then force-kill by PID if still alive
            if (App?.HasExited == false)
            {
                Thread.Sleep(2000);
                App.Kill();
            }
        }
        catch { /* already exited */ }

        Automation.Dispose();
        RestoreSettings();
    }
}
