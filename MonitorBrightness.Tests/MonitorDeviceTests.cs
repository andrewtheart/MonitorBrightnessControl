using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class MonitorDeviceTests
{
    [Fact]
    public void DisplayName_PrefersEdidName()
    {
        var monitor = new MonitorDevice
        {
            Index = 0,
            EdidName = "Dell U2723QE",
            FriendlyName = "Generic PnP Monitor",
            Description = "Physical Monitor"
        };

        Assert.Equal("Dell U2723QE", monitor.DisplayName);
    }

    [Fact]
    public void DisplayName_UsesFriendlyNameWhenEdidNameIsMissingAndFriendlyNameIsSpecific()
    {
        var monitor = new MonitorDevice
        {
            Index = 1,
            FriendlyName = "LG HDR 4K",
            Description = "Physical Monitor"
        };

        Assert.Equal("LG HDR 4K", monitor.DisplayName);
    }

    [Fact]
    public void DisplayName_IgnoresGenericPnPFriendlyNameAndUsesDescription()
    {
        var monitor = new MonitorDevice
        {
            Index = 2,
            FriendlyName = "Generic PnP Monitor",
            Description = "DisplayPort Monitor"
        };

        Assert.Equal("DisplayPort Monitor", monitor.DisplayName);
    }

    [Fact]
    public void DisplayName_FallsBackToDisplayIndex()
    {
        var monitor = new MonitorDevice { Index = 3 };

        Assert.Equal("Monitor 4", monitor.DisplayName);
    }

    [Fact]
    public void Resolution_FormatsWidthAndHeight()
    {
        var monitor = new MonitorDevice { Width = 3840, Height = 2160 };

        Assert.Equal("3840x2160", monitor.Resolution);
    }
}
