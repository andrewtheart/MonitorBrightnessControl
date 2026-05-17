namespace MonitorBrightness.Tests;

public sealed class WindowPositionTests
{
    private const int EdgeOffset = 32;

    // Work area: 0,0 to 1920,1080
    private const int WorkLeft = 0;
    private const int WorkTop = 0;
    private const int WorkRight = 1920;
    private const int WorkBottom = 1080;
    private const int WinW = 580;
    private const int WinH = 400;

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
    public void CalculateWindowPosition_AllPositions_ReturnValidCoords(WindowPosition pos)
    {
        var (x, y) = MainWindow.CalculateWindowPosition(pos, WorkLeft, WorkTop, WorkRight, WorkBottom, WinW, WinH);

        // Window should remain within work area bounds (with some tolerance for edge offset)
        Assert.InRange(x, WorkLeft, WorkRight - 1);
        Assert.InRange(y, WorkTop, WorkBottom - 1);
    }

    [Fact]
    public void CalculateWindowPosition_TopLeft_OffsetsFromTopLeftCorner()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.TopLeft, WorkLeft, WorkTop, WorkRight, WorkBottom, WinW, WinH);

        Assert.Equal(WorkLeft + EdgeOffset, x);
        Assert.Equal(WorkTop + EdgeOffset, y);
    }

    [Fact]
    public void CalculateWindowPosition_TopCenter_CentersHorizontally()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.TopCenter, WorkLeft, WorkTop, WorkRight, WorkBottom, WinW, WinH);

        int expectedX = (WorkRight - WorkLeft - WinW) / 2;
        Assert.Equal(expectedX, x);
        Assert.Equal(WorkTop + EdgeOffset, y);
    }

    [Fact]
    public void CalculateWindowPosition_TopRight_OffsetsFromRightEdge()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.TopRight, WorkLeft, WorkTop, WorkRight, WorkBottom, WinW, WinH);

        Assert.Equal(WorkRight - WinW - EdgeOffset, x);
        Assert.Equal(WorkTop + EdgeOffset, y);
    }

    [Fact]
    public void CalculateWindowPosition_Center_CentersBothAxes()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.Center, WorkLeft, WorkTop, WorkRight, WorkBottom, WinW, WinH);

        Assert.Equal((WorkRight - WinW) / 2, x);
        Assert.Equal((WorkBottom - WinH) / 2, y);
    }

    [Fact]
    public void CalculateWindowPosition_BottomRight_OffsetsFromBottomRight()
    {
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.BottomRight, WorkLeft, WorkTop, WorkRight, WorkBottom, WinW, WinH);

        Assert.Equal(WorkRight - WinW - EdgeOffset, x);
        Assert.Equal(WorkBottom - WinH - EdgeOffset, y);
    }

    [Fact]
    public void CalculateWindowPosition_WithNonZeroWorkAreaOrigin()
    {
        // Simulates a secondary monitor offset at 1920,0
        var (x, y) = MainWindow.CalculateWindowPosition(
            WindowPosition.Center, 1920, 0, 3840, 1080, WinW, WinH);

        Assert.Equal(1920 + (1920 - WinW) / 2, x);
        Assert.Equal((1080 - WinH) / 2, y);
    }

    [Fact]
    public void ShouldRetryMonitorLoad_NoRetries_WhenAllSupportBrightness()
    {
        var monitors = new List<MonitorDevice>
        {
            new() { PhysicalMonitorHandle = (IntPtr)1, SupportsBrightness = true },
            new() { PhysicalMonitorHandle = (IntPtr)2, SupportsBrightness = true },
        };

        Assert.False(MainWindow.ShouldRetryMonitorLoad(0, monitors));
    }

    [Fact]
    public void ShouldRetryMonitorLoad_RetriesWhenPhysicalHandleHasNoBrightness()
    {
        var monitors = new List<MonitorDevice>
        {
            new() { PhysicalMonitorHandle = (IntPtr)1, SupportsBrightness = true },
            new() { PhysicalMonitorHandle = (IntPtr)2, SupportsBrightness = false },
        };

        Assert.True(MainWindow.ShouldRetryMonitorLoad(0, monitors));
        Assert.True(MainWindow.ShouldRetryMonitorLoad(2, monitors));
    }

    [Fact]
    public void ShouldRetryMonitorLoad_StopsAfterMaxRetries()
    {
        var monitors = new List<MonitorDevice>
        {
            new() { PhysicalMonitorHandle = (IntPtr)1, SupportsBrightness = false },
        };

        Assert.False(MainWindow.ShouldRetryMonitorLoad(3, monitors));
    }

    [Fact]
    public void ShouldRetryMonitorLoad_IgnoresZeroHandles()
    {
        var monitors = new List<MonitorDevice>
        {
            new() { PhysicalMonitorHandle = IntPtr.Zero, SupportsBrightness = false },
        };

        Assert.False(MainWindow.ShouldRetryMonitorLoad(0, monitors));
    }

    [Fact]
    public void ShouldRetryMonitorLoad_EmptyList_NoRetry()
    {
        Assert.False(MainWindow.ShouldRetryMonitorLoad(0, new List<MonitorDevice>()));
    }
}
