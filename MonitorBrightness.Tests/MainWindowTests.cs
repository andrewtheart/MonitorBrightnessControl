using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class MainWindowTests
{
    // Work area: left=0, top=0, right=1920, bottom=1080, window 580×400

    [Fact]
    public void CalculateWindowPosition_Center_CentersWindow()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.Center, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal((1920 - 580) / 2, x);
        Assert.Equal((1080 - 400) / 2, y);
    }

    [Fact]
    public void CalculateWindowPosition_TopLeft_OffsetsFromEdge()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.TopLeft, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal(32, x); // EdgeOffset
        Assert.Equal(32, y);
    }

    [Fact]
    public void CalculateWindowPosition_BottomRight_OffsetsFromEdge()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.BottomRight, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal(1920 - 580 - 32, x);
        Assert.Equal(1080 - 400 - 32, y);
    }

    [Fact]
    public void CalculateWindowPosition_TopCenter_CentersHorizontally()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.TopCenter, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal((1920 - 580) / 2, x);
        Assert.Equal(32, y);
    }

    [Fact]
    public void CalculateWindowPosition_MiddleRight_CentersVertically()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.MiddleRight, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal(1920 - 580 - 32, x);
        Assert.Equal((1080 - 400) / 2, y);
    }

    [Fact]
    public void CalculateWindowPosition_BottomCenter_CentersHorizontally()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.BottomCenter, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal((1920 - 580) / 2, x);
        Assert.Equal(1080 - 400 - 32, y);
    }

    [Fact]
    public void CalculateWindowPosition_MiddleLeft_OffsetsFromLeft()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.MiddleLeft, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal(32, x);
        Assert.Equal((1080 - 400) / 2, y);
    }

    [Fact]
    public void CalculateWindowPosition_TopRight_OffsetsFromRight()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.TopRight, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal(1920 - 580 - 32, x);
        Assert.Equal(32, y);
    }

    [Fact]
    public void CalculateWindowPosition_BottomLeft_OffsetsFromBottom()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.BottomLeft, 0, 0, 1920, 1080, 580, 400);

        Assert.Equal(32, x);
        Assert.Equal(1080 - 400 - 32, y);
    }

    [Fact]
    public void CalculateWindowPosition_NonZeroWorkOrigin_OffsetsCorrectly()
    {
        // Simulates a secondary monitor at (1920, 0)
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.Center, 1920, 0, 3840, 1080, 580, 400);

        Assert.Equal(1920 + (1920 - 580) / 2, x);
        Assert.Equal((1080 - 400) / 2, y);
    }

    [Theory]
    [InlineData(WindowPosition.TopLeft)]
    [InlineData(WindowPosition.TopCenter)]
    [InlineData(WindowPosition.TopRight)]
    [InlineData(WindowPosition.MiddleLeft)]
    [InlineData(WindowPosition.Center)]
    [InlineData(WindowPosition.MiddleRight)]
    [InlineData(WindowPosition.BottomLeft)]
    [InlineData(WindowPosition.BottomCenter)]
    [InlineData(WindowPosition.BottomRight)]
    public void CalculateWindowPosition_AllPositions_StayWithinWorkArea(WindowPosition position)
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            position, 0, 0, 1920, 1080, 580, 400);

        Assert.InRange(x, 0, 1920 - 580);
        Assert.InRange(y, 0, 1080 - 400);
    }
}
