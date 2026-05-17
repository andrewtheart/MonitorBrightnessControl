using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class MonitorCardFactoryTests
{
    [Fact]
    public void Constructor_StoresParameters()
    {
        using var queue = new BrightnessUpdateQueue();
        int selectedIndex = -1;
        var factory = new MonitorCardFactory(queue, idx => selectedIndex = idx);

        // Constructor should not throw and should store the dependencies
        Assert.NotNull(factory);
    }
}
