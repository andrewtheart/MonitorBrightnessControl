using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class WindowSizerTests
{
    [Theory]
    [InlineData(800, 700, 750, 850)]   // needs to grow by 50
    [InlineData(800, 700, 600, 700)]   // needs to shrink by 100
    [InlineData(800, 700, 700, 800)]   // already correct size
    public void CalculateOuterHeight_ComputesCorrectTarget(
        int currentOuter, int currentClient, int targetClient, int expectedOuter)
    {
        int result = WindowSizer.CalculateOuterHeight(
            currentOuter, currentClient, targetClient, 580, 580);
        Assert.Equal(expectedOuter, result);
    }

    [Fact]
    public void CalculateOuterHeight_ClampsToMinimum200()
    {
        // currentOuter=300, currentClient=250, targetClient=50 → outer would be 100
        int result = WindowSizer.CalculateOuterHeight(300, 250, 50, 580, 580);
        Assert.Equal(200, result);
    }

    [Theory]
    [InlineData(500, 500, null)]   // exactly equal → null
    [InlineData(500, 499, null)]   // within 1 px → null
    [InlineData(500, 501, null)]   // within 1 px → null
    [InlineData(500, 498, 2)]      // 2 px off → delta=2
    [InlineData(500, 510, -10)]    // 10 px too tall → delta=-10
    [InlineData(500, 400, 100)]    // 100 px too short → delta=100
    public void GetResizeDelta_ReturnsCorrectDelta(int target, int current, int? expected)
    {
        int? result = WindowSizer.GetResizeDelta(target, current);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateListHeight_SingleItem()
    {
        double result = WindowSizer.CalculateListHeight([120.0], visibleCount: 1);
        Assert.Equal(120.0, result);
    }

    [Fact]
    public void CalculateListHeight_MultipleItems_AddsSpacing()
    {
        double result = WindowSizer.CalculateListHeight([100.0, 100.0, 100.0], visibleCount: 3);
        // 300 + 2 * 8 spacing = 316
        Assert.Equal(316.0, result);
    }

    [Fact]
    public void CalculateListHeight_LimitsToVisibleCount()
    {
        double result = WindowSizer.CalculateListHeight([100.0, 100.0, 100.0, 100.0], visibleCount: 2);
        // 200 + 1 * 8 spacing = 208
        Assert.Equal(208.0, result);
    }

    [Fact]
    public void CalculateListHeight_ZeroHeightUsesFallback()
    {
        double result = WindowSizer.CalculateListHeight([0.0, 0.0], visibleCount: 2, fallbackHeight: 80);
        // 160 + 1 * 8 = 168
        Assert.Equal(168.0, result);
    }

    [Fact]
    public void CalculateListHeight_NegativeHeightUsesFallback()
    {
        double result = WindowSizer.CalculateListHeight([-5.0], visibleCount: 1, fallbackHeight: 80);
        Assert.Equal(80.0, result);
    }

    [Fact]
    public void CalculateListHeight_EmptyList_ReturnsZero()
    {
        double result = WindowSizer.CalculateListHeight([], visibleCount: 3);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CalculateListHeight_CustomSpacing()
    {
        double result = WindowSizer.CalculateListHeight([50.0, 50.0], visibleCount: 2, spacing: 16);
        // 100 + 16 = 116
        Assert.Equal(116.0, result);
    }

    [Fact]
    public void CalculateListHeight_CeilsFractionalHeights()
    {
        double result = WindowSizer.CalculateListHeight([100.3, 100.3], visibleCount: 2);
        // 200.6 + 8 = 208.6, ceil = 209
        Assert.Equal(209.0, result);
    }

    [Theory]
    [InlineData(0, 500, 500, false)]   // pass 0, exactly equal → stop
    [InlineData(4, 500, 400, true)]    // pass 4, still off → retry
    [InlineData(5, 500, 400, false)]   // pass 5 → stop (max reached)
    [InlineData(0, 500, 499, false)]   // within 1px → stop
    [InlineData(0, 500, 501, false)]   // within 1px → stop
    [InlineData(3, 600, 500, true)]    // pass 3, 100px off → retry
    public void ShouldRetryAutoSize_ReturnsCorrectly(int pass, int target, int current, bool expected)
    {
        Assert.Equal(expected, WindowSizer.ShouldRetryAutoSize(pass, target, current));
    }

    [Fact]
    public void ShouldRetryAutoSize_CustomMaxPasses()
    {
        Assert.True(WindowSizer.ShouldRetryAutoSize(2, 500, 400, maxPasses: 3));
        Assert.False(WindowSizer.ShouldRetryAutoSize(3, 500, 400, maxPasses: 3));
    }

    [Theory]
    [InlineData(5, 4, 4)]    // more monitors than max → clamped
    [InlineData(3, 4, 3)]    // fewer monitors than max
    [InlineData(0, 4, 1)]    // zero monitors → floored to 1
    [InlineData(0, 0, 1)]    // both zero → floored to 1
    [InlineData(1, 1, 1)]    // exactly one
    public void ClampVisibleCount_ClampsCorrectly(int monitorCount, int max, int expected)
    {
        Assert.Equal(expected, WindowSizer.ClampVisibleCount(monitorCount, max));
    }

    [Theory]
    [InlineData(96u, 1.0)]
    [InlineData(192u, 2.0)]
    [InlineData(144u, 1.5)]
    [InlineData(120u, 1.25)]
    public void CalculateScale_ComputesCorrectly(uint dpi, double expected)
    {
        Assert.Equal(expected, WindowSizer.CalculateScale(dpi));
    }

    [Theory]
    [InlineData(960, 1.0, 960.0)]
    [InlineData(960, 2.0, 480.0)]
    [InlineData(0, 1.0, 1.0)]        // zero width → clamped to 1
    [InlineData(-100, 1.0, 1.0)]     // negative → clamped to 1
    public void CalculateWidthDips_ComputesCorrectly(int pixels, double scale, double expected)
    {
        Assert.Equal(expected, WindowSizer.CalculateWidthDips(pixels, scale));
    }

    [Theory]
    [InlineData(400.0, 1.0, 400)]
    [InlineData(400.0, 2.0, 800)]
    [InlineData(400.5, 1.0, 401)]    // ceiling
    [InlineData(100.0, 1.0, 200)]    // clamped to min 200
    [InlineData(0.0, 1.0, 200)]      // zero → min 200
    public void CalculateTargetClientHeight_ComputesCorrectly(double dips, double scale, int expected)
    {
        Assert.Equal(expected, WindowSizer.CalculateTargetClientHeight(dips, scale));
    }
}
