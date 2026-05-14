using MonitorBrightness;
using Windows.System;

namespace MonitorBrightness.Tests;

public sealed class KeyboardBrightnessControllerTests
{
    #region GetMonitorNumberFromKey

    [Theory]
    [InlineData(VirtualKey.Number0, 0)]
    [InlineData(VirtualKey.Number1, 1)]
    [InlineData(VirtualKey.Number5, 5)]
    [InlineData(VirtualKey.Number9, 9)]
    [InlineData(VirtualKey.NumberPad0, 0)]
    [InlineData(VirtualKey.NumberPad1, 1)]
    [InlineData(VirtualKey.NumberPad9, 9)]
    public void GetMonitorNumberFromKey_NumericKeys_ReturnsNumber(VirtualKey key, int expected)
    {
        Assert.Equal(expected, KeyboardBrightnessController.GetMonitorNumberFromKey(key));
    }

    [Theory]
    [InlineData(VirtualKey.A)]
    [InlineData(VirtualKey.Up)]
    [InlineData(VirtualKey.Escape)]
    [InlineData(VirtualKey.Space)]
    public void GetMonitorNumberFromKey_NonNumericKeys_ReturnsNull(VirtualKey key)
    {
        Assert.Null(KeyboardBrightnessController.GetMonitorNumberFromKey(key));
    }

    #endregion

    #region GetKeyboardStep

    [Theory]
    [InlineData(false, false, 5)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 10)]
    [InlineData(true, true, 25)]
    public void GetKeyboardStep_ModifierCombinations_ReturnsExpectedStep(bool ctrl, bool shift, int expected)
    {
        Assert.Equal(expected, KeyboardBrightnessController.GetKeyboardStep(ctrl, shift));
    }

    [Theory]
    [InlineData(VirtualKey.PageUp, 10)]
    [InlineData(VirtualKey.PageDown, 10)]
    public void GetKeyboardStep_PageKeys_AlwaysReturnsTen(VirtualKey key, int expected)
    {
        Assert.Equal(expected, KeyboardBrightnessController.GetKeyboardStep(key));
    }

    #endregion

    #region IsIncreaseKey / IsDecreaseKey

    [Theory]
    [InlineData(VirtualKey.Up)]
    [InlineData(VirtualKey.Right)]
    [InlineData(VirtualKey.PageUp)]
    public void IsIncreaseKey_IncreaseKeys_ReturnsTrue(VirtualKey key)
    {
        Assert.True(KeyboardBrightnessController.IsIncreaseKey(key));
    }

    [Theory]
    [InlineData(VirtualKey.Down)]
    [InlineData(VirtualKey.Left)]
    [InlineData(VirtualKey.PageDown)]
    public void IsDecreaseKey_DecreaseKeys_ReturnsTrue(VirtualKey key)
    {
        Assert.True(KeyboardBrightnessController.IsDecreaseKey(key));
    }

    [Theory]
    [InlineData(VirtualKey.Down)]
    [InlineData(VirtualKey.Left)]
    [InlineData(VirtualKey.A)]
    [InlineData(VirtualKey.Home)]
    public void IsIncreaseKey_NonIncreaseKeys_ReturnsFalse(VirtualKey key)
    {
        Assert.False(KeyboardBrightnessController.IsIncreaseKey(key));
    }

    [Theory]
    [InlineData(VirtualKey.Up)]
    [InlineData(VirtualKey.Right)]
    [InlineData(VirtualKey.A)]
    [InlineData(VirtualKey.End)]
    public void IsDecreaseKey_NonDecreaseKeys_ReturnsFalse(VirtualKey key)
    {
        Assert.False(KeyboardBrightnessController.IsDecreaseKey(key));
    }

    #endregion

    #region VK_ADD / VK_SUBTRACT / VK_OEM_PLUS / VK_OEM_MINUS

    [Fact]
    public void IsIncreaseKey_VkAdd_ReturnsTrue()
    {
        Assert.True(KeyboardBrightnessController.IsIncreaseKey((VirtualKey)0x6B));
    }

    [Fact]
    public void IsIncreaseKey_VkOemPlus_ReturnsTrue()
    {
        Assert.True(KeyboardBrightnessController.IsIncreaseKey((VirtualKey)0xBB));
    }

    [Fact]
    public void IsDecreaseKey_VkSubtract_ReturnsTrue()
    {
        Assert.True(KeyboardBrightnessController.IsDecreaseKey((VirtualKey)0x6D));
    }

    [Fact]
    public void IsDecreaseKey_VkOemMinus_ReturnsTrue()
    {
        Assert.True(KeyboardBrightnessController.IsDecreaseKey((VirtualKey)0xBD));
    }

    #endregion
}
