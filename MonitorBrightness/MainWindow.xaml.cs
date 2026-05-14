using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI;
using Windows.Graphics;
using Windows.System;
using System.Runtime.InteropServices;
using WinRT.Interop;
using Microsoft.UI.Windowing;

namespace MonitorBrightness;

public sealed partial class MainWindow : Window
{
    private List<MonitorDevice> _monitors = new();
    private readonly List<MonitorUi> _monitorControls = new();
    private AppSettings _settings;
    private HotkeyManager _hotkeyManager;
    private TrayIconManager _trayManager;
    private BrightnessUpdateQueue _brightnessUpdates;
    private MonitorCardFactory _cardFactory;
    private WindowSizer _windowSizer;
    private KeyboardBrightnessController _keyboardController;
    private IdentifyOverlayManager _identifyOverlays;
    private IntPtr _hwnd;
    private bool _isRecordingHotkey;
    private bool _isClosing;

    // Win32 interop for window messages
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _newWndProc;
    private IntPtr _oldWndProc;

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int GWL_WNDPROC = -4;
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;
    private const int DefaultWindowWidth = 580;

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "Monitor Brightness Control";
        this.AppWindow.Resize(new SizeInt32(DefaultWindowWidth, 400));

        _settings = AppSettings.Load();
        _hotkeyManager = new HotkeyManager();
        _trayManager = new TrayIconManager();
        _brightnessUpdates = new BrightnessUpdateQueue();
        _keyboardController = new KeyboardBrightnessController(_monitorControls);
        _identifyOverlays = new IdentifyOverlayManager();

        _hwnd = WindowNative.GetWindowHandle(this);

        _cardFactory = new MonitorCardFactory(_brightnessUpdates, _keyboardController.SelectMonitor);
        _windowSizer = new WindowSizer(this, _hwnd, DefaultWindowWidth);

        // Extend content into title bar for seamless look
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);

        // Set caption button colors to blend with dark background
        if (this.AppWindow.TitleBar is not null)
        {
            var titleBar = this.AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(255, 45, 45, 55);
            titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(255, 60, 60, 70);
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = ColorHelper.FromArgb(255, 120, 120, 120);
        }

        // Set window icon
        SetWindowIcon();

        // Setup tray icon
        _trayManager.Create(_hwnd);
        _trayManager.OnTrayIconClicked += RestoreWindow;
        _trayManager.OnTrayIconCloseClicked += CloseApp;

        // Subclass the window to intercept messages
        SubclassWindow();

        // Register hotkey if previously configured
        _hotkeyManager.SetWindow(_hwnd);
        if (_settings.HasHotkey)
        {
            _hotkeyManager.Register((uint)_settings.HotkeyModifiers, (uint)_settings.HotkeyVirtualKey);
            HotkeyTextBox.Text = _settings.HotkeyDisplayText;
        }

        // Intercept close to minimize to tray instead
        this.AppWindow.Closing += AppWindow_Closing;
        this.Activated += OnFirstActivated;

        RootGrid.Focus(FocusState.Programmatic);
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        this.Activated -= OnFirstActivated;

        // Defer monitor enumeration until after the window is fully active.
        // Some monitors (e.g. LS27A600U) fail DDC/CI queries when called
        // too early during window construction.
        DispatcherQueue.TryEnqueue(() =>
        {
            LoadMonitors();
            RootGrid.Focus(FocusState.Programmatic);

            // Position window according to settings (or CLI overrides)
            ApplyStartPosition(
                App.OverridePosition ?? _settings.StartPosition,
                App.OverrideDisplay ?? _settings.StartDisplay);

            if (!_settings.FirstLaunchHotkeyDialogShown)
            {
                _ = ShowFirstLaunchDialog();
            }
        });
    }

    private void SubclassWindow()
    {
        _newWndProc = new WndProcDelegate(NewWindowProc);
        _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private void SetWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(iconPath))
        {
            this.AppWindow.SetIcon(iconPath);
        }
    }

    private IntPtr NewWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            // Handle global hotkey
            if (msg == HotkeyManager.WM_HOTKEY && _hotkeyManager.IsHotkeyMessage(wParam))
            {
                RestoreWindow();
                return IntPtr.Zero;
            }

            // Handle tray icon messages
            if (_trayManager.HandleMessage(msg, wParam, lParam))
            {
                return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn("Unhandled exception while processing window message", ex);
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void RestoreWindow()
    {
        ShowWindow(_hwnd, SW_RESTORE);
        ShowWindow(_hwnd, SW_SHOW);
        SetForegroundWindow(_hwnd);
        RootGrid.Focus(FocusState.Programmatic);

        // Also use AppWindow to ensure visibility
        if (this.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsAlwaysOnTop = false;
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isClosing) return;

        if (_settings.CloseToTray)
        {
            args.Cancel = true;
            ShowWindow(_hwnd, SW_HIDE);
        }
        else
        {
            CloseApp();
        }
    }

    private void CloseApp()
    {
        _isClosing = true;
        _identifyOverlays.Dismiss();
        _brightnessUpdates.Dispose();
        MonitorEnumerator.ReleaseMonitors(_monitors);
        _hotkeyManager.Dispose();
        _trayManager.Dispose();
        this.Close();
    }

    private async Task ShowFirstLaunchDialog()
    {
        // Small delay to ensure window is fully loaded
        await Task.Delay(500);

        _settings.FirstLaunchHotkeyDialogShown = true;
        _settings.Save();

        bool openSettings = await DialogWindow.ShowTwoButton(
            _hwnd,
            "Welcome!",
            "By default, closing the window minimizes the app to the system tray instead of exiting.\n\n" +
            "You can also assign a global hotkey to quickly bring this app to the foreground from anywhere.\n\n" +
            "Would you like to change these settings now?",
            "Open Settings",
            "Keep defaults");

        if (openSettings)
        {
            ShowSettingsPanel();
        }
    }

    #region Settings Panel

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsPanel();
    }

    private async void KeyboardHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var helpText = string.Join(Environment.NewLine, new[]
            {
                "Select a target:",
                "  1-9: select a monitor",
                "  0 or A: select all brightness-capable monitors",
                "",
                "Adjust brightness:",
                "  Up / Right / +: increase",
                "  Down / Left / -: decrease",
                "  PageUp / PageDown: adjust by 10%",
                "  Home / End: set to min/max",
                "",
                "Step sizes:",
                "  Default: 5%",
                "  Ctrl: 1%",
                "  Shift: 10%",
                "  Ctrl+Shift: 25%"
            });

        var content = new TextBlock
        {
            Text = helpText,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Opacity = 0.9,
        };

        await DialogWindow.ShowCloseOnly(
            _hwnd,
            "Keyboard brightness controls",
            content,
            "Got it",
            width: 460,
            height: 440);

        RootGrid.Focus(FocusState.Programmatic);
    }

    private void ShowSettingsPanel()
    {
        SettingsOverlay.Visibility = Visibility.Visible;
        if (_settings.HasHotkey)
        {
            HotkeyTextBox.Text = _settings.HotkeyDisplayText;
            HotkeyStatus.Text = "✓ Hotkey is active";
        }
        CloseToTrayToggle.IsOn = _settings.CloseToTray;
        MaxMonitorsNumberBox.Value = _settings.MaxVisibleMonitors;

        // Select the matching position item
        var posTag = _settings.StartPosition.ToString();
        for (int i = 0; i < StartPositionComboBox.Items.Count; i++)
        {
            if (StartPositionComboBox.Items[i] is ComboBoxItem item && item.Tag is string tag && tag == posTag)
            {
                StartPositionComboBox.SelectedIndex = i;
                break;
            }
        }

        StartDisplayNumberBox.Value = _settings.StartDisplay;

        _windowSizer.ResizeToContent(RootGrid);
    }

    private void CloseToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _settings.CloseToTray = CloseToTrayToggle.IsOn;
        _settings.Save();
    }

    private void SettingsBack_Click(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        _isRecordingHotkey = false;
        _windowSizer.ResizeToFit(RootGrid, MonitorPanel, MonitorScrollViewer, _monitors.Count, _settings.MaxVisibleMonitors);
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = true;
        HotkeyTextBox.Text = "";
        HotkeyStatus.Text = "Press a key combination (e.g., Ctrl+Shift+B)...";
    }

    private void HotkeyTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isRecordingHotkey) return;

        e.Handled = true;

        // Ignore standalone modifier keys
        var key = e.Key;
        if (key == VirtualKey.Control || key == VirtualKey.Shift ||
            key == VirtualKey.Menu || key == VirtualKey.LeftWindows ||
            key == VirtualKey.RightWindows)
            return;

        // Build modifier flags from current keyboard state
        uint modifiers = 0;
        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
        var winState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows);

        if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) modifiers |= HotkeyManager.MOD_CONTROL;
        if (shiftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) modifiers |= HotkeyManager.MOD_SHIFT;
        if (altState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) modifiers |= HotkeyManager.MOD_ALT;
        if (winState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) modifiers |= HotkeyManager.MOD_WIN;

        // Require at least one modifier
        if (modifiers == 0)
        {
            HotkeyStatus.Text = "⚠ Please include at least one modifier (Ctrl, Alt, Shift, or Win)";
            return;
        }

        uint vk = (uint)key;
        var displayText = HotkeyManager.ToDisplayString(modifiers, vk);

        // Try to register
        if (_hotkeyManager.Register(modifiers, vk))
        {
            _settings.HotkeyModifiers = (int)modifiers;
            _settings.HotkeyVirtualKey = (int)vk;
            _settings.HotkeyDisplayText = displayText;
            _settings.Save();

            HotkeyTextBox.Text = displayText;
            HotkeyStatus.Text = "✓ Hotkey registered successfully!";
        }
        else
        {
            HotkeyStatus.Text = "⚠ Could not register that hotkey (may be in use by another app)";
        }

        _isRecordingHotkey = false;
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        _hotkeyManager.Unregister();
        _settings.HotkeyModifiers = 0;
        _settings.HotkeyVirtualKey = 0;
        _settings.HotkeyDisplayText = "";
        _settings.Save();

        HotkeyTextBox.Text = "";
        HotkeyStatus.Text = "Hotkey cleared";
        _isRecordingHotkey = false;
    }

    private void MaxMonitorsNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        _settings.MaxVisibleMonitors = (int)args.NewValue;
        _settings.Save();
        _windowSizer.ResizeToFit(RootGrid, MonitorPanel, MonitorScrollViewer, _monitors.Count, _settings.MaxVisibleMonitors);
    }

    private void StartPositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StartPositionComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            Enum.TryParse<WindowPosition>(tag, out var pos))
        {
            _settings.StartPosition = pos;
            _settings.Save();
        }
    }

    private void StartDisplayNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        _settings.StartDisplay = (int)args.NewValue;
        _settings.Save();
    }

    #endregion

    #region Monitor Controls

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (SettingsOverlay.Visibility == Visibility.Visible)
            return;

        e.Handled = _keyboardController.HandleKeyDown(e);
    }

    private int _monitorRetryCount;

    private void LoadMonitors()
    {
        _brightnessUpdates.Dispose();
        _brightnessUpdates = new BrightnessUpdateQueue();
        _cardFactory = new MonitorCardFactory(_brightnessUpdates, _keyboardController.SelectMonitor);
        MonitorEnumerator.ReleaseMonitors(_monitors);
        MonitorPanel.Children.Clear();
        _monitorControls.Clear();
        _monitors = MonitorEnumerator.GetMonitors();

        // If any monitor with a physical handle failed DDC/CI brightness detection,
        // schedule an automatic retry. Some monitors need the handles to be released
        // and re-acquired with the message pump running in between.
        if (_monitorRetryCount < 3 &&
            _monitors.Any(m => m.PhysicalMonitorHandle != IntPtr.Zero && !m.SupportsBrightness))
        {
            _monitorRetryCount++;
            _ = RetryLoadMonitorsAsync();
        }
        else
        {
            _monitorRetryCount = 0;
        }

        if (_monitors.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            _windowSizer.ResizeToFit(RootGrid, MonitorPanel, MonitorScrollViewer, 0, _settings.MaxVisibleMonitors);
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        foreach (var monitor in _monitors)
        {
            var (element, control) = _cardFactory.Create(monitor);
            MonitorPanel.Children.Add(element);
            _monitorControls.Add(control);
        }

        _keyboardController.SelectFirstBrightnessCapable();
        _windowSizer.ResizeToFit(RootGrid, MonitorPanel, MonitorScrollViewer, _monitors.Count, _settings.MaxVisibleMonitors);
    }

    private async Task RetryLoadMonitorsAsync()
    {
        await Task.Delay(1000);
        LoadMonitors();
    }

    #region Window Positioning

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

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const int EdgeOffset = 32; // pixels offset from screen edges

    /// <summary>
    /// Positions the window according to the given position and display settings.
    /// </summary>
    internal void ApplyStartPosition(WindowPosition position, int displayNumber)
    {
        var workAreas = new List<RECT>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref info))
                workAreas.Add(info.rcWork);
            return true;
        }, IntPtr.Zero);

        if (workAreas.Count == 0)
            return;

        // displayNumber: 0 = primary (first), 1-based otherwise
        int idx = displayNumber > 0 ? Math.Min(displayNumber - 1, workAreas.Count - 1) : 0;
        var work = workAreas[idx];

        var windowSize = this.AppWindow.Size;
        int w = windowSize.Width;
        int h = windowSize.Height;

        int areaW = work.Right - work.Left;
        int areaH = work.Bottom - work.Top;

        int x = position switch
        {
            WindowPosition.TopLeft or WindowPosition.MiddleLeft or WindowPosition.BottomLeft
                => work.Left + EdgeOffset,
            WindowPosition.TopCenter or WindowPosition.Center or WindowPosition.BottomCenter
                => work.Left + (areaW - w) / 2,
            WindowPosition.TopRight or WindowPosition.MiddleRight or WindowPosition.BottomRight
                => work.Right - w - EdgeOffset,
            _ => work.Left + (areaW - w) / 2,
        };

        int y = position switch
        {
            WindowPosition.TopLeft or WindowPosition.TopCenter or WindowPosition.TopRight
                => work.Top + EdgeOffset,
            WindowPosition.MiddleLeft or WindowPosition.Center or WindowPosition.MiddleRight
                => work.Top + (areaH - h) / 2,
            WindowPosition.BottomLeft or WindowPosition.BottomCenter or WindowPosition.BottomRight
                => work.Bottom - h - EdgeOffset,
            _ => work.Top + (areaH - h) / 2,
        };

        this.AppWindow.Move(new PointInt32(x, y));
    }

    #endregion

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadMonitors();
    }

    private void IdentifyButton_Click(object sender, RoutedEventArgs e)
    {
        _identifyOverlays.Show(_monitors, DispatcherQueue);
    }

    #endregion
}
