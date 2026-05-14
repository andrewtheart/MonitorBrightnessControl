using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class AppSettingsTests
{
    [Theory]
    [InlineData(-10, AppSettings.MinVisibleMonitors)]
    [InlineData(0, AppSettings.MinVisibleMonitors)]
    [InlineData(1, AppSettings.MinVisibleMonitors)]
    [InlineData(7, 7)]
    [InlineData(999, AppSettings.MaxVisibleMonitorsLimit)]
    public void Normalize_ClampsMaxVisibleMonitors(int input, int expected)
    {
        var settings = new AppSettings { MaxVisibleMonitors = input };

        settings.Normalize();

        Assert.Equal(expected, settings.MaxVisibleMonitors);
    }

    [Fact]
    public void Normalize_ClearsModifierAndDisplayText_WhenVirtualKeyIsUnset()
    {
        var settings = new AppSettings
        {
            HotkeyModifiers = (int)HotkeyManager.MOD_CONTROL,
            HotkeyVirtualKey = 0,
            HotkeyDisplayText = "Ctrl+B"
        };

        settings.Normalize();

        Assert.False(settings.HasHotkey);
        Assert.Equal(0, settings.HotkeyModifiers);
        Assert.Equal("", settings.HotkeyDisplayText);
    }

    [Fact]
    public void Normalize_PreservesValidHotkey()
    {
        var settings = new AppSettings
        {
            HotkeyModifiers = (int)(HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT),
            HotkeyVirtualKey = 'B',
            HotkeyDisplayText = "Ctrl+Shift+B"
        };

        settings.Normalize();

        Assert.True(settings.HasHotkey);
        Assert.Equal((int)(HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT), settings.HotkeyModifiers);
        Assert.Equal('B', settings.HotkeyVirtualKey);
        Assert.Equal("Ctrl+Shift+B", settings.HotkeyDisplayText);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsSettings_AndRemovesTempFile()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings", "monitor_brightness_settings.json");
        var settings = new AppSettings
        {
            MinimizeNotificationShown = true,
            FirstLaunchHotkeyDialogShown = true,
            HotkeyModifiers = (int)HotkeyManager.MOD_ALT,
            HotkeyVirtualKey = 'M',
            HotkeyDisplayText = "Alt+M",
            MaxVisibleMonitors = 5
        };

        settings.Save(settingsPath);
        var loaded = AppSettings.Load(settingsPath);

        Assert.True(loaded.MinimizeNotificationShown);
        Assert.True(loaded.FirstLaunchHotkeyDialogShown);
        Assert.Equal((int)HotkeyManager.MOD_ALT, loaded.HotkeyModifiers);
        Assert.Equal('M', loaded.HotkeyVirtualKey);
        Assert.Equal("Alt+M", loaded.HotkeyDisplayText);
        Assert.Equal(5, loaded.MaxVisibleMonitors);
        Assert.True(File.Exists(settingsPath));
        Assert.False(File.Exists(settingsPath + ".tmp"));
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = Path.Combine(directory.Path, "missing.json");

        var settings = AppSettings.Load(settingsPath);

        Assert.False(settings.MinimizeNotificationShown);
        Assert.False(settings.FirstLaunchHotkeyDialogShown);
        Assert.False(settings.HasHotkey);
        Assert.Equal(AppSettings.DefaultMaxVisibleMonitors, settings.MaxVisibleMonitors);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaults()
    {
        using var directory = new TemporaryDirectory();
        var settingsPath = Path.Combine(directory.Path, "monitor_brightness_settings.json");
        File.WriteAllText(settingsPath, "{ invalid json");

        var settings = AppSettings.Load(settingsPath);

        Assert.False(settings.HasHotkey);
        Assert.Equal(AppSettings.DefaultMaxVisibleMonitors, settings.MaxVisibleMonitors);
    }
}
