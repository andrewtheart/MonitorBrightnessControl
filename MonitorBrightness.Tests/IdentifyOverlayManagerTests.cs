using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class IdentifyOverlayManagerTests
{
    [Fact]
    public void Dismiss_OnFreshInstance_DoesNotThrow()
    {
        var manager = new IdentifyOverlayManager();
        manager.Dismiss(); // timer is null, window list is empty
    }

    [Fact]
    public void Dismiss_CalledTwice_DoesNotThrow()
    {
        var manager = new IdentifyOverlayManager();
        manager.Dismiss();
        manager.Dismiss();
    }
}
