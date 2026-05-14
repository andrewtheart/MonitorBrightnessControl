using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class HotkeyManagerTests
{
    [Theory]
    [InlineData("Ctrl+Shift+B", HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT, (uint)'B')]
    [InlineData("control + alt + m", HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT, (uint)'M')]
    [InlineData("Win+F12", HotkeyManager.MOD_WIN, 0x7B)]
    [InlineData("Shift+PageDown", HotkeyManager.MOD_SHIFT, 0x22)]
    [InlineData("Alt+Esc", HotkeyManager.MOD_ALT, 0x1B)]
    public void ParseHotkeyString_ParsesModifiersAndKeys(string text, uint expectedModifiers, uint expectedVirtualKey)
    {
        var (modifiers, virtualKey) = HotkeyManager.ParseHotkeyString(text);

        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedVirtualKey, virtualKey);
    }

    [Fact]
    public void ParseHotkeyString_UnknownKeyReturnsZeroVirtualKey()
    {
        var (modifiers, virtualKey) = HotkeyManager.ParseHotkeyString("Ctrl+DefinitelyNotAKey");

        Assert.Equal(HotkeyManager.MOD_CONTROL, modifiers);
        Assert.Equal(0u, virtualKey);
    }

    [Theory]
    [InlineData(HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_SHIFT, (uint)'B', "Ctrl+Shift+B")]
    [InlineData(HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT | HotkeyManager.MOD_WIN, 0x70u, "Ctrl+Alt+Win+F1")]
    [InlineData(HotkeyManager.MOD_SHIFT, 0x22u, "Shift+PageDown")]
    [InlineData(0u, 0xABu, "0xAB")]
    public void ToDisplayString_FormatsModifiersAndKeys(uint modifiers, uint virtualKey, string expected)
    {
        Assert.Equal(expected, HotkeyManager.ToDisplayString(modifiers, virtualKey));
    }

    [Fact]
    public void Register_WithNoWindowHandle_ReturnsFalse()
    {
        using var manager = new HotkeyManager();

        Assert.False(manager.Register(HotkeyManager.MOD_CONTROL, 'B'));
    }
}
