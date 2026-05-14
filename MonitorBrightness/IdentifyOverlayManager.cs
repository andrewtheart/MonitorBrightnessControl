using Microsoft.UI.Xaml;

namespace MonitorBrightness;

/// <summary>
/// Manages the identify overlay windows that show monitor numbers.
/// </summary>
internal sealed class IdentifyOverlayManager
{
    private readonly List<Window> _identifyWindows = new();
    private DispatcherTimer? _identifyTimer;

    public void Show(IReadOnlyList<MonitorDevice> monitors, Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
    {
        Dismiss();

        foreach (var monitor in monitors)
        {
            var win = new IdentifyWindow(monitor);
            win.Activate();
            _identifyWindows.Add(win);
        }

        _identifyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _identifyTimer.Tick += (s, args) =>
        {
            _identifyTimer.Stop();
            Dismiss();
        };
        _identifyTimer.Start();
    }

    public void Dismiss()
    {
        _identifyTimer?.Stop();
        foreach (var win in _identifyWindows)
        {
            try
            {
                win.Close();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Unable to close identify overlay", ex);
            }
        }
        _identifyWindows.Clear();
    }
}
