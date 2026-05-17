using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace MonitorBrightness;

/// <summary>
/// Handles keyboard-driven monitor selection and brightness adjustment.
/// </summary>
internal sealed class KeyboardBrightnessController
{
    private readonly List<MonitorUi> _monitorControls;
    private int? _selectedMonitorIndex;
    private bool _allMonitorsSelected;

    public KeyboardBrightnessController(List<MonitorUi> monitorControls)
    {
        _monitorControls = monitorControls;
    }

    public int? SelectedMonitorIndex => _selectedMonitorIndex;
    public bool AllMonitorsSelected => _allMonitorsSelected;

    public bool HandleKeyDown(KeyRoutedEventArgs e)
    {
        int? monitorNumber = GetMonitorNumberFromKey(e.Key);
        if (monitorNumber.HasValue)
        {
            if (monitorNumber.Value == 0)
                SelectAllMonitors();
            else
                SelectMonitor(monitorNumber.Value - 1);
            return true;
        }

        int keyCode = (int)e.Key;
        if (e.Key == VirtualKey.A)
        {
            SelectAllMonitors();
        }
        else if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Right || e.Key == VirtualKey.PageUp ||
                 keyCode == 0x6B || keyCode == 0xBB)
        {
            AdjustSelectedBrightness(GetKeyboardStep(e.Key));
        }
        else if (e.Key == VirtualKey.Down || e.Key == VirtualKey.Left || e.Key == VirtualKey.PageDown ||
                 keyCode == 0x6D || keyCode == 0xBD)
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
            return false;
        }
        return true;
    }

    public void SelectMonitor(int monitorIndex)
    {
        if (!_monitorControls.Any(control => control.Monitor.Index == monitorIndex))
            return;

        _selectedMonitorIndex = monitorIndex;
        _allMonitorsSelected = false;
        UpdateSelectionVisuals();
    }

    public void SelectAllMonitors()
    {
        if (!_monitorControls.Any(control => control.Slider is not null))
            return;

        _selectedMonitorIndex = null;
        _allMonitorsSelected = true;
        UpdateSelectionVisuals();
    }

    public void SelectFirstBrightnessCapable()
    {
        var first = _monitorControls.FirstOrDefault(c => c.Slider is not null);
        if (first is not null)
        {
            _selectedMonitorIndex = first.Monitor.Index;
            _allMonitorsSelected = false;
            UpdateSelectionVisuals();
        }
    }

    public void UpdateSelectionVisuals()
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

    internal static int? GetMonitorNumberFromKey(VirtualKey key)
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

    internal static int GetKeyboardStep(VirtualKey key)
    {
        if (key == VirtualKey.PageUp || key == VirtualKey.PageDown)
            return 10;

        bool ctrl = IsKeyDown(VirtualKey.Control);
        bool shift = IsKeyDown(VirtualKey.Shift);

        return GetKeyboardStep(ctrl, shift);
    }

    internal static int GetKeyboardStep(bool ctrl, bool shift)
    {
        if (ctrl && shift)
            return 25;
        if (shift)
            return 10;
        if (ctrl)
            return 1;
        return 5;
    }

    internal static bool IsIncreaseKey(VirtualKey key)
    {
        int keyCode = (int)key;
        return key == VirtualKey.Up || key == VirtualKey.Right || key == VirtualKey.PageUp ||
               keyCode == 0x6B || keyCode == 0xBB;
    }

    internal static bool IsDecreaseKey(VirtualKey key)
    {
        int keyCode = (int)key;
        return key == VirtualKey.Down || key == VirtualKey.Left || key == VirtualKey.PageDown ||
               keyCode == 0x6D || keyCode == 0xBD;
    }
}
