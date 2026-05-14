using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonitorBrightness;

/// <summary>
/// Persists app settings to a JSON file next to the executable.
/// </summary>
public class AppSettings
{
    public const int DefaultMaxVisibleMonitors = 4;
    public const int MinVisibleMonitors = 1;
    public const int MaxVisibleMonitorsLimit = 20;

    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory, "monitor_brightness_settings.json");

    public bool FirstLaunchHotkeyDialogShown { get; set; }
    public bool CloseToTray { get; set; } = true;
    public int HotkeyModifiers { get; set; } // Win32 MOD_ flags
    public int HotkeyVirtualKey { get; set; } // Win32 VK code
    public string HotkeyDisplayText { get; set; } = "";
    public int MaxVisibleMonitors { get; set; } = DefaultMaxVisibleMonitors;

    public static AppSettings Load()
    {
        return Load(SettingsPath);
    }

    internal static AppSettings Load(string settingsPath)
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
                settings.Normalize();
                return settings;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn("Unable to load settings; using defaults", ex);
        }
        return new AppSettings();
    }

    public void Save()
    {
        Save(SettingsPath);
    }

    internal void Save(string settingsPath)
    {
        try
        {
            Normalize();
            var json = JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings);
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempPath = settingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, settingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            AppLogger.Warn("Unable to save settings", ex);
        }
    }

    public bool HasHotkey => HotkeyVirtualKey != 0;

    public void Normalize()
    {
        MaxVisibleMonitors = Math.Clamp(MaxVisibleMonitors, MinVisibleMonitors, MaxVisibleMonitorsLimit);

        if (HotkeyVirtualKey == 0)
        {
            HotkeyModifiers = 0;
            HotkeyDisplayText = "";
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext
{
}
