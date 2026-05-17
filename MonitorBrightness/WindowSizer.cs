using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System.Runtime.InteropServices;

namespace MonitorBrightness;

/// <summary>
/// Handles auto-sizing the main window to fit its content.
/// </summary>
internal sealed class WindowSizer
{
    private readonly Window _window;
    private readonly IntPtr _hwnd;
    private readonly int _defaultWidth;
    private int _targetClientHeightPixels;

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out CLIENT_RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private struct CLIENT_RECT
    {
        public int Left, Top, Right, Bottom;
    }

    public WindowSizer(Window window, IntPtr hwnd, int defaultWidth)
    {
        _window = window;
        _hwnd = hwnd;
        _defaultWidth = defaultWidth;
    }

    /// <summary>
    /// Computes the target outer window height given the current outer height,
    /// the current client height, and the desired client height.
    /// </summary>
    internal static int CalculateOuterHeight(int currentOuterHeight, int currentClientHeight, int targetClientHeight, int defaultWidth, int currentWidth)
    {
        int outerTarget = currentOuterHeight + (targetClientHeight - currentClientHeight);
        return Math.Max(outerTarget, 200);
    }

    /// <summary>
    /// Returns how many pixels the window still needs to grow or shrink,
    /// or null when the window is already within 1 px of the target.
    /// </summary>
    internal static int? GetResizeDelta(int targetClientHeight, int currentClientHeight)
    {
        int delta = targetClientHeight - currentClientHeight;
        return Math.Abs(delta) <= 1 ? null : delta;
    }

    public void ResizeToFit(Grid rootGrid, StackPanel monitorPanel, ScrollViewer scrollViewer, int monitorCount, int maxVisibleMonitors)
    {
        int visibleCount = ClampVisibleCount(monitorCount, maxVisibleMonitors);

        _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            AutoSizeToContent(rootGrid, monitorPanel, scrollViewer, visibleCount, monitorCount > maxVisibleMonitors, 0);
        });
    }

    public void ResizeToContent(Grid rootGrid)
    {
        _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            rootGrid.UpdateLayout();

            var dpi = GetDpiForWindow(_hwnd);
            double scale = dpi / 96.0;
            double widthDips = Math.Max(GetClientWidthPixels() / scale, 1);

            rootGrid.Measure(new Windows.Foundation.Size(widthDips, double.PositiveInfinity));
            int targetHeight = Math.Max((int)Math.Ceiling(rootGrid.DesiredSize.Height * scale), 200);

            var currentSize = _window.AppWindow.Size;
            int clientHeight = GetClientHeightPixels();
            int outerTargetHeight = currentSize.Height + (targetHeight - clientHeight);
            if (Math.Abs(clientHeight - targetHeight) > 1)
                _window.AppWindow.Resize(new SizeInt32(Math.Max(currentSize.Width, _defaultWidth), Math.Max(outerTargetHeight, 200)));
        });
    }

    private void AutoSizeToContent(Grid rootGrid, StackPanel monitorPanel, ScrollViewer scrollViewer, int visibleCount, bool shouldScroll, int pass)
    {
        rootGrid.UpdateLayout();
        monitorPanel.UpdateLayout();

        if (shouldScroll)
        {
            scrollViewer.Height = GetVisibleMonitorListHeight(monitorPanel, scrollViewer, visibleCount);
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        else
        {
            scrollViewer.Height = double.NaN;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }

        scrollViewer.InvalidateMeasure();
        rootGrid.InvalidateMeasure();

        var dpi = GetDpiForWindow(_hwnd);
        double scale = dpi / 96.0;
        double widthDips = Math.Max(GetClientWidthPixels() / scale, 1);

        rootGrid.Measure(new Windows.Foundation.Size(widthDips, double.PositiveInfinity));
        _targetClientHeightPixels = Math.Max((int)Math.Ceiling(rootGrid.DesiredSize.Height * scale), 200);

        var currentSize = _window.AppWindow.Size;
        int clientHeight = GetClientHeightPixels();
        int outerTargetHeight = currentSize.Height + (_targetClientHeightPixels - clientHeight);
        if (Math.Abs(clientHeight - _targetClientHeightPixels) > 1)
            _window.AppWindow.Resize(new SizeInt32(Math.Max(currentSize.Width, _defaultWidth), Math.Max(outerTargetHeight, 200)));

        _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            VerifyAutoSize(rootGrid, shouldScroll, pass));
    }

    private static double GetVisibleMonitorListHeight(StackPanel monitorPanel, ScrollViewer scrollViewer, int visibleCount)
    {
        double totalHeight = 0;
        int count = 0;
        foreach (FrameworkElement child in monitorPanel.Children.Cast<FrameworkElement>())
        {
            if (count >= visibleCount)
                break;

            child.UpdateLayout();
            double height = child.ActualHeight;
            if (height <= 0)
            {
                child.Measure(new Windows.Foundation.Size(scrollViewer.ActualWidth, double.PositiveInfinity));
                height = child.DesiredSize.Height;
            }

            totalHeight += height > 0 ? height : 80;
            count++;
        }

        if (count > 1)
            totalHeight += (count - 1) * 8;

        return Math.Ceiling(totalHeight);
    }

    /// <summary>
    /// Pure calculation: sum the first <paramref name="visibleCount"/> item heights
    /// (using <paramref name="fallbackHeight"/> when an item reports zero),
    /// add inter-item spacing, and ceiling the result.
    /// </summary>
    internal static double CalculateListHeight(
        IReadOnlyList<double> itemHeights, int visibleCount, double spacing = 8, double fallbackHeight = 80)
    {
        double totalHeight = 0;
        int count = 0;
        foreach (var height in itemHeights)
        {
            if (count >= visibleCount)
                break;
            totalHeight += height > 0 ? height : fallbackHeight;
            count++;
        }
        if (count > 1)
            totalHeight += (count - 1) * spacing;
        return Math.Ceiling(totalHeight);
    }

    /// <summary>
    /// Returns true when the auto-size loop should continue (pass limit not reached
    /// and the client height still differs from the target by more than 1 px).
    /// </summary>
    internal static bool ShouldRetryAutoSize(int pass, int targetClientHeight, int currentClientHeight, int maxPasses = 5)
    {
        if (pass >= maxPasses)
            return false;
        return Math.Abs(targetClientHeight - currentClientHeight) > 1;
    }

    /// <summary>
    /// Clamps the visible monitor count: at least 1, at most maxVisibleMonitors.
    /// </summary>
    internal static int ClampVisibleCount(int monitorCount, int maxVisibleMonitors)
    {
        int count = Math.Min(monitorCount, maxVisibleMonitors);
        return count == 0 ? 1 : count;
    }

    /// <summary>
    /// Converts a DPI value to a scale factor (96 DPI = 1.0×).
    /// </summary>
    internal static double CalculateScale(uint dpi) => dpi / 96.0;

    /// <summary>
    /// Converts a client-area width in pixels to DIPs, floored at 1.
    /// </summary>
    internal static double CalculateWidthDips(int clientWidthPixels, double scale)
        => Math.Max(clientWidthPixels / scale, 1);

    /// <summary>
    /// Converts a desired height in DIPs to pixels, clamped to a minimum of 200.
    /// </summary>
    internal static int CalculateTargetClientHeight(double desiredHeightDips, double scale)
        => Math.Max((int)Math.Ceiling(desiredHeightDips * scale), 200);

    private void VerifyAutoSize(Grid rootGrid, bool shouldScroll, int pass)
    {
        if (pass >= 5)
            return;

        int clientHeight = GetClientHeightPixels();
        int deltaPixels = _targetClientHeightPixels - clientHeight;
        if (Math.Abs(deltaPixels) <= 1)
            return;

        var currentSize = _window.AppWindow.Size;
        _window.AppWindow.Resize(new SizeInt32(Math.Max(currentSize.Width, _defaultWidth), Math.Max(currentSize.Height + deltaPixels, 200)));

        _window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            VerifyAutoSize(rootGrid, shouldScroll, pass + 1));
    }

    private int GetClientHeightPixels()
    {
        return GetClientRect(_hwnd, out var rect) ? rect.Bottom - rect.Top : _window.AppWindow.Size.Height;
    }

    private int GetClientWidthPixels()
    {
        return GetClientRect(_hwnd, out var rect) ? rect.Right - rect.Left : _window.AppWindow.Size.Width;
    }
}
