using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class DialogWindowTests
{
    [Fact]
    public void CalculateCenter_CentersDialogOnOwner()
    {
        // Owner at (100, 100) to (500, 400), dialog 200×150
        var (x, y) = DialogWindow.CalculateCenter(100, 100, 500, 400, 200, 150);

        // Owner center = (300, 250), dialog should be at (200, 175)
        Assert.Equal(200, x);
        Assert.Equal(175, y);
    }

    [Fact]
    public void CalculateCenter_OwnerAtOrigin()
    {
        var (x, y) = DialogWindow.CalculateCenter(0, 0, 800, 600, 480, 320);

        Assert.Equal((400 - 240), x); // 160
        Assert.Equal((300 - 160), y); // 140
    }

    [Fact]
    public void CalculateCenter_SmallDialog()
    {
        var (x, y) = DialogWindow.CalculateCenter(200, 200, 600, 500, 100, 50);

        // Owner center = (400, 350)
        Assert.Equal(350, x);
        Assert.Equal(325, y);
    }

    [Fact]
    public void CalculateCenter_OwnerOnSecondaryMonitor()
    {
        // Owner on monitor 2 at (1920, 100) to (2400, 500)
        var (x, y) = DialogWindow.CalculateCenter(1920, 100, 2400, 500, 480, 320);

        // Owner center = (2160, 300)
        Assert.Equal(2160 - 240, x); // 1920
        Assert.Equal(300 - 160, y);  // 140
    }

    [Fact]
    public void CalculateCenter_ExactlyMatchingSize_ReturnsOwnerOrigin()
    {
        var (x, y) = DialogWindow.CalculateCenter(100, 100, 300, 300, 200, 200);

        // Owner center = (200, 200), dialog half = (100, 100) → (100, 100)
        Assert.Equal(100, x);
        Assert.Equal(100, y);
    }

    [Fact]
    public void CalculateCenter_ZeroSizeDialog()
    {
        var (x, y) = DialogWindow.CalculateCenter(0, 0, 1000, 800, 0, 0);

        Assert.Equal(500, x);
        Assert.Equal(400, y);
    }
}
