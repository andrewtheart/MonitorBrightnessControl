using MonitorBrightness;

namespace MonitorBrightness.Tests;

public sealed class TrayIconManagerTests
{
    [Fact]
    public void HandleMessage_NonTrayMessage_ReturnsFalse()
    {
        using var tray = new TrayIconManager();
        Assert.False(tray.HandleMessage(0x0001, IntPtr.Zero, IntPtr.Zero));
    }

    [Fact]
    public void HandleMessage_TrayLeftButtonUp_FiresClickedEvent()
    {
        using var tray = new TrayIconManager();
        bool clicked = false;
        tray.OnTrayIconClicked += () => clicked = true;

        bool handled = tray.HandleMessage(
            (uint)TrayIconManager.WM_TRAYICON,
            IntPtr.Zero,
            (IntPtr)TrayIconManager.WM_LBUTTONUP);

        Assert.True(handled);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleMessage_TrayDoubleClick_FiresClickedEvent()
    {
        using var tray = new TrayIconManager();
        bool clicked = false;
        tray.OnTrayIconClicked += () => clicked = true;

        bool handled = tray.HandleMessage(
            (uint)TrayIconManager.WM_TRAYICON,
            IntPtr.Zero,
            (IntPtr)TrayIconManager.WM_LBUTTONDBLCLK);

        Assert.True(handled);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleMessage_TrayLeftButtonUp_NoSubscriber_StillReturnsTrue()
    {
        using var tray = new TrayIconManager();

        bool handled = tray.HandleMessage(
            (uint)TrayIconManager.WM_TRAYICON,
            IntPtr.Zero,
            (IntPtr)TrayIconManager.WM_LBUTTONUP);

        Assert.True(handled);
    }

    [Fact]
    public void HandleMessage_TrayUnknownMouseMessage_ReturnsFalse()
    {
        using var tray = new TrayIconManager();

        // Send WM_TRAYICON but with a mouse message that isn't LButton/DblClk/RButton
        bool handled = tray.HandleMessage(
            (uint)TrayIconManager.WM_TRAYICON,
            IntPtr.Zero,
            (IntPtr)0x0200); // WM_MOUSEMOVE

        Assert.False(handled);
    }

    [Fact]
    public void Dispose_WithoutCreate_DoesNotThrow()
    {
        var tray = new TrayIconManager();
        tray.Dispose(); // Should not throw since _added is false
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var tray = new TrayIconManager();
        tray.Dispose();
        tray.Dispose();
    }

    [Fact]
    public void ExecuteContextMenuCommand_OpenCommand_FiresClickedEvent()
    {
        using var tray = new TrayIconManager();
        bool clicked = false;
        tray.OnTrayIconClicked += () => clicked = true;

        tray.ExecuteContextMenuCommand(TrayIconManager.ContextMenuOpen);

        Assert.True(clicked);
    }

    [Fact]
    public void ExecuteContextMenuCommand_CloseCommand_FiresCloseEvent()
    {
        using var tray = new TrayIconManager();
        bool closed = false;
        tray.OnTrayIconCloseClicked += () => closed = true;

        tray.ExecuteContextMenuCommand(TrayIconManager.ContextMenuClose);

        Assert.True(closed);
    }

    [Fact]
    public void ExecuteContextMenuCommand_UnknownCommand_DoesNotFire()
    {
        using var tray = new TrayIconManager();
        bool anyFired = false;
        tray.OnTrayIconClicked += () => anyFired = true;
        tray.OnTrayIconCloseClicked += () => anyFired = true;

        tray.ExecuteContextMenuCommand(0);     // no selection
        tray.ExecuteContextMenuCommand(9999);  // unknown command

        Assert.False(anyFired);
    }

    [Fact]
    public void ExecuteContextMenuCommand_NoSubscribers_DoesNotThrow()
    {
        using var tray = new TrayIconManager();
        tray.ExecuteContextMenuCommand(TrayIconManager.ContextMenuOpen);
        tray.ExecuteContextMenuCommand(TrayIconManager.ContextMenuClose);
    }

    [Fact]
    public void Create_WithZeroHandle_DoesNotThrow()
    {
        // Create with invalid hwnd - P/Invoke calls will fail gracefully
        // but the code path through icon loading and Shell_NotifyIcon is exercised
        var tray = new TrayIconManager();
        tray.Create(IntPtr.Zero);
        tray.Dispose(); // Should clean up the loaded icon handle
    }

    [Fact]
    public void Create_ThenDispose_CleansUpIcon()
    {
        var tray = new TrayIconManager();
        tray.Create(IntPtr.Zero);
        // Dispose calls Remove (returns early since _added=false from failed Shell_NotifyIcon)
        // then destroys the icon handle loaded from shell32.dll
        tray.Dispose();
        // Second dispose should be safe
        tray.Dispose();
    }
}
