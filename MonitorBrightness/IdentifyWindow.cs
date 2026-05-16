using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;
using System.Runtime.InteropServices;

namespace MonitorBrightness;

/// <summary>
/// A borderless overlay window that shows the monitor number badge,
/// centered on the target monitor. The badge fills the entire window
/// so there is no visible background frame.
/// </summary>
public sealed partial class IdentifyWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_BORDER = 0x00800000;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;

    // Badge dimensions
    private const int BadgeWidth = 360;
    private const int BadgeHeight = 320;

    public IdentifyWindow(MonitorDevice monitor)
    {
        // Badge fills the entire window — no separate background visible
        var grid = new Grid
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(240, 30, 30, 55)),
            Padding = new Thickness(40, 24, 40, 24),
        };

        var numberStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var numberText = new TextBlock
        {
            Text = (monitor.Index + 1).ToString(),
            FontSize = 140,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 96, 200, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        numberStack.Children.Add(numberText);

        var nameText = new TextBlock
        {
            Text = monitor.DisplayName,
            FontSize = 22,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 280,
            Margin = new Thickness(0, 4, 0, 0),
        };
        numberStack.Children.Add(nameText);

        var resText = new TextBlock
        {
            Text = monitor.Resolution,
            FontSize = 14,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 160, 160, 160)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        };
        numberStack.Children.Add(resText);

        grid.Children.Add(numberStack);

        grid.PointerPressed += (s, e) => this.Close();

        this.Content = grid;
        this.Title = "";

        // Position and style the window
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
        }

        // Strip all window frame styles to remove the border
        int style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER);
        SetWindowLong(hwnd, GWL_STYLE, style);

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER);

        // Center the badge on the monitor
        int centerX = monitor.Left + (monitor.Width - BadgeWidth) / 2;
        int centerY = monitor.Top + (monitor.Height - BadgeHeight) / 2;
        appWindow.MoveAndResize(new RectInt32(centerX, centerY, BadgeWidth, BadgeHeight));
    }
}
