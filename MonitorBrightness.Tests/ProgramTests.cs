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
}
