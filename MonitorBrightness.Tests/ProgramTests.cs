using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void ParseGuiOverrides_EmptyArgs_ReturnsNulls()
    {
        var (position, display) = Program.ParseGuiOverrides([]);
        Assert.Null(position);
        Assert.Null(display);
    }

    [Theory]
    [InlineData("--position", "Center", WindowPosition.Center)]
    [InlineData("--pos", "TopLeft", WindowPosition.TopLeft)]
    [InlineData("-p", "BottomRight", WindowPosition.BottomRight)]
    [InlineData("/position", "MiddleLeft", WindowPosition.MiddleLeft)]
    [InlineData("--position", "topright", WindowPosition.TopRight)]
    public void ParseGuiOverrides_Position_ParsesCorrectly(string flag, string value, WindowPosition expected)
    {
        var (position, display) = Program.ParseGuiOverrides([flag, value]);
        Assert.Equal(expected, position);
        Assert.Null(display);
    }

    [Theory]
    [InlineData("--display", "1", 1)]
    [InlineData("-d", "2", 2)]
    [InlineData("/d", "0", 0)]
    public void ParseGuiOverrides_Display_ParsesCorrectly(string flag, string value, int expected)
    {
        var (position, display) = Program.ParseGuiOverrides([flag, value]);
        Assert.Null(position);
        Assert.Equal(expected, display);
    }

    [Fact]
    public void ParseGuiOverrides_BothFlags_ParsesBoth()
    {
        var (position, display) = Program.ParseGuiOverrides(["--position", "TopCenter", "--display", "3"]);
        Assert.Equal(WindowPosition.TopCenter, position);
        Assert.Equal(3, display);
    }

    [Fact]
    public void ParseGuiOverrides_InvalidPosition_ReturnsNull()
    {
        var (position, display) = Program.ParseGuiOverrides(["--position", "NotAPosition"]);
        Assert.Null(position);
        Assert.Null(display);
    }

    [Fact]
    public void ParseGuiOverrides_InvalidDisplay_ReturnsNull()
    {
        var (position, display) = Program.ParseGuiOverrides(["--display", "abc"]);
        Assert.Null(position);
        Assert.Null(display);
    }

    [Fact]
    public void ParseGuiOverrides_FlagWithoutValue_IgnoresFlag()
    {
        var (position, display) = Program.ParseGuiOverrides(["--position"]);
        Assert.Null(position);
        Assert.Null(display);
    }

    [Fact]
    public void ParseGuiOverrides_UnknownFlags_Ignored()
    {
        var (position, display) = Program.ParseGuiOverrides(["--verbose", "--position", "Center"]);
        Assert.Equal(WindowPosition.Center, position);
        Assert.Null(display);
    }

    #region ParseDemoCount

    [Fact]
    public void ParseDemoCount_NoFlag_ReturnsZero()
    {
        Assert.Equal(0, Program.ParseDemoCount([]));
        Assert.Equal(0, Program.ParseDemoCount(["--position", "Center"]));
    }

    [Fact]
    public void ParseDemoCount_WithValidCount_ReturnsCount()
    {
        Assert.Equal(3, Program.ParseDemoCount(["--demo", "3"]));
        Assert.Equal(1, Program.ParseDemoCount(["--demo", "1"]));
        Assert.Equal(5, Program.ParseDemoCount(["--demo", "5"]));
    }

    [Fact]
    public void ParseDemoCount_ClampsToRange()
    {
        Assert.Equal(1, Program.ParseDemoCount(["--demo", "0"]));
        Assert.Equal(20, Program.ParseDemoCount(["--demo", "100"]));
    }

    [Fact]
    public void ParseDemoCount_InvalidValue_ReturnsZero()
    {
        Assert.Equal(0, Program.ParseDemoCount(["--demo", "abc"]));
        Assert.Equal(0, Program.ParseDemoCount(["--demo"]));
    }

    [Fact]
    public void ParseDemoCount_MixedWithOtherFlags()
    {
        Assert.Equal(4, Program.ParseDemoCount(["--position", "Center", "--demo", "4", "--display", "1"]));
    }

    #endregion

    #region CreateDemoMonitors

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void CreateDemoMonitors_ReturnsCorrectCount(int count)
    {
        var monitors = Program.CreateDemoMonitors(count);
        Assert.Equal(count, monitors.Count);
    }

    [Fact]
    public void CreateDemoMonitors_AllSupportBrightness()
    {
        var monitors = Program.CreateDemoMonitors(3);
        Assert.All(monitors, m => Assert.True(m.SupportsBrightness));
    }

    [Fact]
    public void CreateDemoMonitors_HaveZeroPhysicalHandles()
    {
        var monitors = Program.CreateDemoMonitors(3);
        Assert.All(monitors, m => Assert.Equal(IntPtr.Zero, m.PhysicalMonitorHandle));
    }

    [Fact]
    public void CreateDemoMonitors_HaveSequentialIndices()
    {
        var monitors = Program.CreateDemoMonitors(4);
        for (int i = 0; i < monitors.Count; i++)
            Assert.Equal(i, monitors[i].Index);
    }

    [Fact]
    public void CreateDemoMonitors_HaveValidBrightnessRange()
    {
        var monitors = Program.CreateDemoMonitors(5);
        Assert.All(monitors, m =>
        {
            Assert.Equal(0, m.MinBrightness);
            Assert.Equal(100, m.MaxBrightness);
            Assert.InRange(m.CurrentBrightness, 0, 100);
        });
    }

    [Fact]
    public void CreateDemoMonitors_HaveNonEmptyNames()
    {
        var monitors = Program.CreateDemoMonitors(3);
        Assert.All(monitors, m => Assert.False(string.IsNullOrEmpty(m.EdidName)));
    }

    [Fact]
    public void CreateDemoMonitors_HaveValidResolution()
    {
        var monitors = Program.CreateDemoMonitors(3);
        Assert.All(monitors, m =>
        {
            Assert.True(m.Width > 0);
            Assert.True(m.Height > 0);
        });
    }

    #endregion
}
