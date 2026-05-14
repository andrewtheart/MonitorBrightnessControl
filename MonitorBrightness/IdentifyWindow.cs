using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Composition;

namespace MonitorBrightness;

/// <summary>
/// A borderless overlay window that shows just the monitor number badge,
/// centered on the target monitor with no background.
/// </summary>
public sealed partial class IdentifyWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left, Right, Top, Bottom;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;

    // Badge dimensions
    private const int BadgeWidth = 280;
    private const int BadgeHeight = 320;

    public IdentifyWindow(MonitorDevice monitor)
    {
        var grid = new Grid
        {
            Background = new SolidColorBrush(Colors.Transparent),
        };

        // The visible badge
        var numberBorder = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(240, 30, 30, 55)),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(40, 24, 40, 24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var numberStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

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

        numberBorder.Child = numberStack;
        grid.Children.Add(numberBorder);

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

        // Center the badge on the monitor
        int centerX = monitor.Left + (monitor.Width - BadgeWidth) / 2;
        int centerY = monitor.Top + (monitor.Height - BadgeHeight) / 2;
        appWindow.MoveAndResize(new RectInt32(centerX, centerY, BadgeWidth, BadgeHeight));

        // Make window fully transparent using DWM
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }
}
