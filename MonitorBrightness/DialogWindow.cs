using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace MonitorBrightness;

/// <summary>
/// A standalone dialog window whose size is independent of any parent window.
/// </summary>
internal sealed class DialogWindow
{
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private readonly TaskCompletionSource<bool> _tcs = new();
    private Window? _window;
    private bool _result;

    /// <summary>
    /// Shows a dialog with a primary and secondary button. Returns true if primary was clicked.
    /// </summary>
    public static async Task<bool> ShowTwoButton(
        IntPtr ownerHwnd,
        string title,
        string message,
        string primaryText,
        string secondaryText,
        int width = 480,
        int height = 320)
    {
        var dlg = new DialogWindow();
        return await dlg.Show(ownerHwnd, title, message, primaryText, secondaryText, null, width, height);
    }

    /// <summary>
    /// Shows a dialog with only a close button.
    /// </summary>
    public static async Task ShowCloseOnly(
        IntPtr ownerHwnd,
        string title,
        UIElement content,
        string closeText,
        int width = 480,
        int height = 420)
    {
        var dlg = new DialogWindow();
        await dlg.Show(ownerHwnd, title, null, null, null, closeText, width, height, content);
    }

    private async Task<bool> Show(
        IntPtr ownerHwnd,
        string title,
        string? message,
        string? primaryText,
        string? secondaryText,
        string? closeText,
        int width,
        int height,
        UIElement? customContent = null)
    {
        _window = new Window();
        _window.Title = title;
        _window.ExtendsContentIntoTitleBar = true;

        var hwnd = WindowNative.GetWindowHandle(_window);

        // Remove resize and maximize
        if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        // Size
        _window.AppWindow.Resize(new SizeInt32(width, height));

        // Center on the same monitor as the owner
        CenterOnOwnerWindow(hwnd, ownerHwnd, width, height);

        // Title bar colors
        if (_window.AppWindow.TitleBar is not null)
        {
            var tb = _window.AppWindow.TitleBar;
            tb.ButtonBackgroundColor = Colors.Transparent;
            tb.ButtonInactiveBackgroundColor = Colors.Transparent;
            tb.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 45, 45, 55);
            tb.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 60, 60, 70);
            tb.ButtonForegroundColor = Colors.White;
        }

        // Build content
        var rootPanel = new StackPanel
        {
            Spacing = 16,
            Padding = new Thickness(28, 0, 28, 24),
            Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"],
        };

        // Title bar drag region
        var titleBar = new Grid { Height = 16, Background = new SolidColorBrush(Colors.Transparent) };
        _window.SetTitleBar(titleBar);
        rootPanel.Children.Add(titleBar);

        // Title
        rootPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        // Content
        if (customContent is not null)
        {
            rootPanel.Children.Add(customContent);
        }
        else if (message is not null)
        {
            rootPanel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Opacity = 0.9,
            });
        }

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };

        if (primaryText is not null)
        {
            var primary = new Button
            {
                Content = primaryText,
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 160,
            };
            primary.Click += (_, _) => { _result = true; _window.Close(); };
            buttonPanel.Children.Add(primary);
        }

        if (secondaryText is not null)
        {
            var secondary = new Button
            {
                Content = secondaryText,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 160,
            };
            secondary.Click += (_, _) => { _result = false; _window.Close(); };
            buttonPanel.Children.Add(secondary);
        }

        if (closeText is not null)
        {
            var close = new Button
            {
                Content = closeText,
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 160,
            };
            close.Click += (_, _) => { _result = false; _window.Close(); };
            buttonPanel.Children.Add(close);
        }

        rootPanel.Children.Add(buttonPanel);

        _window.Content = rootPanel;
        _window.Closed += (_, _) => _tcs.TrySetResult(true);

        _window.Activate();
        SetForegroundWindow(hwnd);
        await _tcs.Task;
        return _result;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    private static void CenterOnOwnerWindow(IntPtr dialogHwnd, IntPtr ownerHwnd, int w, int h)
    {
        if (!GetWindowRect(ownerHwnd, out var ownerRect))
            return;

        int ownerCenterX = (ownerRect.Left + ownerRect.Right) / 2;
        int ownerCenterY = (ownerRect.Top + ownerRect.Bottom) / 2;

        int x = ownerCenterX - w / 2;
        int y = ownerCenterY - h / 2;

        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(dialogHwnd));
        appWindow.Move(new PointInt32(x, y));
    }
}
