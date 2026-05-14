using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
    private readonly List<Window> _identifyWindows = new();
    private DispatcherTimer? _identifyTimer;
    private AppSettings _settings;
    private HotkeyManager _hotkeyManager;
    private TrayIconManager _trayManager;
    private BrightnessUpdateQueue _brightnessUpdates;
    private IntPtr _hwnd;
    private bool _isRecordingHotkey;
    private bool _isClosing;
    private int _targetClientHeightPixels;
    private int? _selectedMonitorIndex;
    private bool _allMonitorsSelected;

    private sealed class MonitorUi
    {
        public required MonitorDevice Monitor { get; init; }
        public required Border Card { get; init; }
        public Slider? Slider { get; init; }
        public TextBlock? PercentLabel { get; init; }
    }

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
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out CLIENT_RECT lpRect);

    private const int GWL_WNDPROC = -4;
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;
    private const int DefaultWindowWidth = 680;

    private struct CLIENT_RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "Monitor Brightness Control";
        this.AppWindow.Resize(new SizeInt32(DefaultWindowWidth, 400));

        _settings = AppSettings.Load();
        _hotkeyManager = new HotkeyManager();
        _trayManager = new TrayIconManager();
        _brightnessUpdates = new BrightnessUpdateQueue();

        _hwnd = WindowNative.GetWindowHandle(this);

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
        this.Activated += (_, _) => RootGrid.Focus(FocusState.Programmatic);

        LoadMonitors();
        RootGrid.Focus(FocusState.Programmatic);

        // Show first-launch hotkey dialog
        if (!_settings.FirstLaunchHotkeyDialogShown)
        {
            _ = ShowFirstLaunchDialog();
        }
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

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isClosing) return;

        args.Cancel = true;

        if (!_settings.MinimizeNotificationShown)
        {
            _settings.MinimizeNotificationShown = true;
            _settings.Save();

            var dialog = new ContentDialog
            {
                Title = "Minimize to System Tray",
                Content = "The app will minimize to the system tray instead of closing.\n\n" +
                          "Click or double-click the tray icon to restore it.\n\n" +
                          "Would you like to minimize to tray, or close the app entirely?",
                PrimaryButtonText = "Minimize to Tray",
                SecondaryButtonText = "Close App",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                CloseApp();
                return;
            }
        }

        // Hide window (minimize to tray)
        ShowWindow(_hwnd, SW_HIDE);
    }

    private void CloseApp()
    {
        _isClosing = true;
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

        var dialog = new ContentDialog
        {
            Title = "Welcome!",
            Content = "You can assign a global hotkey to quickly bring this app to the foreground from anywhere.\n\n" +
                      "Would you like to set up a hotkey now?",
            PrimaryButtonText = "Yes, set up hotkey",
            SecondaryButtonText = "Not now",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
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
        var dialog = new ContentDialog
        {
            Title = "Keyboard brightness controls",
            Content = string.Join(Environment.NewLine, new[]
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
            }),
            CloseButtonText = "Got it",
            XamlRoot = this.Content.XamlRoot,
        };

        await dialog.ShowAsync();
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
        MaxMonitorsNumberBox.Value = _settings.MaxVisibleMonitors;
    }

    private void SettingsBack_Click(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        _isRecordingHotkey = false;
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
        ResizeWindowToFit(_monitors.Count);
    }

    #endregion

    #region Monitor Controls

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (SettingsOverlay.Visibility == Visibility.Visible)
            return;

        int? monitorNumber = GetMonitorNumberFromKey(e.Key);
        if (monitorNumber.HasValue)
        {
            if (monitorNumber.Value == 0)
                SelectAllMonitors();
            else
                SelectMonitor(monitorNumber.Value - 1);

            e.Handled = true;
            return;
        }

        int keyCode = (int)e.Key;
        bool handled = true;
        if (e.Key == VirtualKey.A)
        {
            SelectAllMonitors();
        }
        else if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Right || e.Key == VirtualKey.PageUp ||
                 keyCode == 0x6B || keyCode == 0xBB) // VK_ADD / VK_OEM_PLUS
        {
            AdjustSelectedBrightness(GetKeyboardStep(e.Key));
        }
        else if (e.Key == VirtualKey.Down || e.Key == VirtualKey.Left || e.Key == VirtualKey.PageDown ||
                 keyCode == 0x6D || keyCode == 0xBD) // VK_SUBTRACT / VK_OEM_MINUS
        {
            AdjustSelectedBrightness(-GetKeyboardStep(e.Key));
        }
        else if (e.Key == VirtualKey.Home)
        {
            SetSelectedBrightnessToLimit(useMaximum: false);
        }
        else if (e.Key == VirtualKey.End)
        {
            SetSelectedBrightnessToLimit(useMaximum: true);
        }
        else
        {
            handled = false;
        }

        e.Handled = handled;
    }

    private static int? GetMonitorNumberFromKey(VirtualKey key)
    {
        int keyCode = (int)key;
        if (keyCode >= 0x30 && keyCode <= 0x39)
            return keyCode - 0x30;
        if (keyCode >= 0x60 && keyCode <= 0x69)
            return keyCode - 0x60;
        return null;
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        return Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }

    private static int GetKeyboardStep(VirtualKey key)
    {
        if (key == VirtualKey.PageUp || key == VirtualKey.PageDown)
            return 10;

        bool ctrl = IsKeyDown(VirtualKey.Control);
        bool shift = IsKeyDown(VirtualKey.Shift);

        if (ctrl && shift) return 25;
        if (shift) return 10;
        if (ctrl) return 1;
        return 5;
    }

    private void SelectMonitor(int monitorIndex)
    {
        if (!_monitorControls.Any(control => control.Monitor.Index == monitorIndex))
            return;

        _selectedMonitorIndex = monitorIndex;
        _allMonitorsSelected = false;
        UpdateMonitorSelectionVisuals();
    }

    private void SelectAllMonitors()
    {
        if (!_monitorControls.Any(control => control.Slider is not null))
            return;

        _selectedMonitorIndex = null;
        _allMonitorsSelected = true;
        UpdateMonitorSelectionVisuals();
    }

    private IEnumerable<MonitorUi> GetSelectedMonitorControls()
    {
        if (_allMonitorsSelected)
            return _monitorControls.Where(control => control.Slider is not null);

        if (_selectedMonitorIndex.HasValue)
        {
            return _monitorControls.Where(control =>
                control.Monitor.Index == _selectedMonitorIndex.Value && control.Slider is not null);
        }

        return Enumerable.Empty<MonitorUi>();
    }

    private void AdjustSelectedBrightness(int delta)
    {
        foreach (var control in GetSelectedMonitorControls())
        {
            var slider = control.Slider!;
            int next = Math.Clamp((int)Math.Round(slider.Value) + delta,
                control.Monitor.MinBrightness, control.Monitor.MaxBrightness);
            slider.Value = next;
        }
    }

    private void SetSelectedBrightnessToLimit(bool useMaximum)
    {
        foreach (var control in GetSelectedMonitorControls())
        {
            control.Slider!.Value = useMaximum
                ? control.Monitor.MaxBrightness
                : control.Monitor.MinBrightness;
        }
    }

    private void UpdateMonitorSelectionVisuals()
    {
        var selectedBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 96, 200, 255));
        var defaultBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

        foreach (var control in _monitorControls)
        {
            bool isSelected = _allMonitorsSelected
                ? control.Slider is not null
                : _selectedMonitorIndex.HasValue && control.Monitor.Index == _selectedMonitorIndex.Value;
            control.Card.BorderBrush = isSelected ? selectedBrush : defaultBrush;
            control.Card.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
        }
    }

    private void LoadMonitors()
    {
        _brightnessUpdates.Dispose();
        _brightnessUpdates = new BrightnessUpdateQueue();
        MonitorEnumerator.ReleaseMonitors(_monitors);
        MonitorPanel.Children.Clear();
        _monitorControls.Clear();
        _monitors = MonitorEnumerator.GetMonitors();

        if (_monitors.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            ResizeWindowToFit(0);
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        foreach (var monitor in _monitors)
        {
            MonitorPanel.Children.Add(CreateMonitorCard(monitor));
        }

        if (_monitorControls.Any(control => control.Slider is not null))
        {
            _selectedMonitorIndex = _monitorControls.First(control => control.Slider is not null).Monitor.Index;
            _allMonitorsSelected = false;
            UpdateMonitorSelectionVisuals();
        }

        ResizeWindowToFit(_monitors.Count);
    }

    private void ResizeWindowToFit(int monitorCount)
    {
        int visibleCount = Math.Min(monitorCount, _settings.MaxVisibleMonitors);
        if (visibleCount == 0) visibleCount = 1;

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            AutoSizeWindowToContent(visibleCount, monitorCount > _settings.MaxVisibleMonitors, 0);
        });
    }

    private void AutoSizeWindowToContent(int visibleCount, bool shouldScroll, int pass)
    {
        RootGrid.UpdateLayout();
        MonitorPanel.UpdateLayout();

        if (shouldScroll)
        {
            MonitorScrollViewer.Height = GetVisibleMonitorListHeight(visibleCount);
            MonitorScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        else
        {
            // When all tiles should fit, do not turn the ScrollViewer into a viewport.
            // Let it measure to full content height so the last card cannot be clipped.
            MonitorScrollViewer.Height = double.NaN;
            MonitorScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }

        MonitorScrollViewer.InvalidateMeasure();
        RootGrid.InvalidateMeasure();

        var dpi = GetDpiForWindow(_hwnd);
        double scale = dpi / 96.0;
        double widthDips = Math.Max(GetClientWidthPixels() / scale, 1);

        RootGrid.Measure(new Windows.Foundation.Size(widthDips, double.PositiveInfinity));
        _targetClientHeightPixels = Math.Max((int)Math.Ceiling(RootGrid.DesiredSize.Height * scale), 200);

        var currentSize = this.AppWindow.Size;
        int clientHeight = GetClientHeightPixels();
        int outerTargetHeight = currentSize.Height + (_targetClientHeightPixels - clientHeight);
        if (Math.Abs(clientHeight - _targetClientHeightPixels) > 1)
            this.AppWindow.Resize(new SizeInt32(Math.Max(currentSize.Width, DefaultWindowWidth), Math.Max(outerTargetHeight, 200)));

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            VerifyAutoSize(shouldScroll, pass));
    }

    private double GetVisibleMonitorListHeight(int visibleCount)
    {
        double totalHeight = 0;
        int count = 0;
        foreach (FrameworkElement child in MonitorPanel.Children.Cast<FrameworkElement>())
        {
            if (count >= visibleCount) break;

            child.UpdateLayout();
            double height = child.ActualHeight;
            if (height <= 0)
            {
                child.Measure(new Windows.Foundation.Size(MonitorScrollViewer.ActualWidth, double.PositiveInfinity));
                height = child.DesiredSize.Height;
            }

            totalHeight += height > 0 ? height : 80;
            count++;
        }

        if (count > 1)
            totalHeight += (count - 1) * 8; // StackPanel spacing between visible cards only.

        return Math.Ceiling(totalHeight);
    }

    private void VerifyAutoSize(bool shouldScroll, int pass)
    {
        if (pass >= 5)
            return;

        int clientHeight = GetClientHeightPixels();
        int deltaPixels = _targetClientHeightPixels - clientHeight;
        if (Math.Abs(deltaPixels) <= 1)
            return;

        var currentSize = this.AppWindow.Size;
        this.AppWindow.Resize(new SizeInt32(Math.Max(currentSize.Width, DefaultWindowWidth), Math.Max(currentSize.Height + deltaPixels, 200)));

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            VerifyAutoSize(shouldScroll, pass + 1));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private int GetClientHeightPixels()
    {
        return GetClientRect(_hwnd, out var rect) ? rect.Bottom - rect.Top : this.AppWindow.Size.Height;
    }

    private int GetClientWidthPixels()
    {
        return GetClientRect(_hwnd, out var rect) ? rect.Right - rect.Left : this.AppWindow.Size.Width;
    }

    private UIElement CreateMonitorCard(MonitorDevice monitor)
    {
        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
        };
        card.PointerPressed += (_, _) => SelectMonitor(monitor.Index);

        var stack = new StackPanel { Spacing = 4 };
        Slider? slider = null;
        TextBlock? pctText = null;

        // Single row: badge + name + resolution
        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 96, 165, 250)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 1, 6, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = (monitor.Index + 1).ToString(),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
            }
        };
        Grid.SetColumn(badge, 0);
        topRow.Children.Add(badge);

        var nameBlock = new TextBlock
        {
            Text = monitor.DisplayName,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(nameBlock, 1);
        topRow.Children.Add(nameBlock);

        var detailsBlock = new TextBlock
        {
            Text = $"{monitor.Resolution}",
            Opacity = 0.5,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Grid.SetColumn(detailsBlock, 2);
        topRow.Children.Add(detailsBlock);

        stack.Children.Add(topRow);

        if (!monitor.SupportsBrightness)
        {
            var warning = new TextBlock
            {
                Text = "⚠ DDC/CI not supported",
                Opacity = 0.6,
                FontSize = 11,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                Margin = new Thickness(0, 2, 0, 0),
            };
            stack.Children.Add(warning);
        }
        else
        {
            // Compact brightness slider row
            var sliderRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            slider = new Slider
            {
                Minimum = monitor.MinBrightness,
                Maximum = monitor.MaxBrightness,
                Value = monitor.CurrentBrightness,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 32,
            };

            pctText = new TextBlock
            {
                Text = $"{monitor.CurrentBrightness}%",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                MinWidth = 40,
                TextAlignment = TextAlignment.Right,
            };

            slider.ValueChanged += (s, e) =>
            {
                var val = (int)e.NewValue;
                monitor.CurrentBrightness = val;
                pctText.Text = $"{val}%";
                _brightnessUpdates.SetLatest(monitor.PhysicalMonitorHandle, monitor.DisplayName, val);
            };

            Grid.SetColumn(slider, 0);
            Grid.SetColumn(pctText, 1);
            sliderRow.Children.Add(slider);
            sliderRow.Children.Add(pctText);
            stack.Children.Add(sliderRow);
        }

        card.Child = stack;
        _monitorControls.Add(new MonitorUi
        {
            Monitor = monitor,
            Card = card,
            Slider = slider,
            PercentLabel = pctText
        });
        return card;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadMonitors();
    }

    private void IdentifyButton_Click(object sender, RoutedEventArgs e)
    {
        DismissIdentifyOverlays();

        foreach (var monitor in _monitors)
        {
            var win = new IdentifyWindow(monitor);
            win.Activate();
            _identifyWindows.Add(win);
        }

        _identifyTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _identifyTimer.Tick += (s, args) =>
        {
            _identifyTimer.Stop();
            DismissIdentifyOverlays();
        };
        _identifyTimer.Start();
    }

    private void DismissIdentifyOverlays()
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

    #endregion
}
