using System.Runtime.InteropServices;

namespace MonitorBrightness;

/// <summary>
/// Manages global hotkey registration via Win32 RegisterHotKey/UnregisterHotKey.
/// </summary>
public class HotkeyManager : IDisposable
{
    private const int HOTKEY_ID = 9000;
    private IntPtr _hwnd;
    private bool _registered;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifier constants
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;

    public void SetWindow(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public bool Register(uint modifiers, uint virtualKey)
    {
        Unregister();
        if (_hwnd == IntPtr.Zero) return false;
        _registered = RegisterHotKey(_hwnd, HOTKEY_ID, modifiers | MOD_NOREPEAT, virtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
    }

    public bool IsHotkeyMessage(IntPtr wParam)
    {
        return wParam.ToInt32() == HOTKEY_ID;
    }

    public void Dispose()
    {
        Unregister();
    }

    /// <summary>
    /// Convert a display string like "Ctrl+Shift+B" to modifiers and VK code.
    /// </summary>
    public static (uint modifiers, uint vk) ParseHotkeyString(string text)
    {
        uint mods = 0;
        uint vk = 0;
        var parts = text.Split('+').Select(p => p.Trim()).ToArray();

        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    mods |= MOD_CONTROL;
                    break;
                case "ALT":
                    mods |= MOD_ALT;
                    break;
                case "SHIFT":
                    mods |= MOD_SHIFT;
                    break;
                case "WIN":
                case "WINDOWS":
                    mods |= MOD_WIN;
                    break;
                default:
                    vk = VirtualKeyFromString(part);
                    break;
            }
        }
        return (mods, vk);
    }

    /// <summary>
    /// Build a display string from modifiers and VK code.
    /// </summary>
    public static string ToDisplayString(uint modifiers, uint vk)
    {
        var parts = new List<string>();
        if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(VirtualKeyToString(vk));
        return string.Join("+", parts);
    }

    private static uint VirtualKeyFromString(string key)
    {
        if (key.Length == 1 && char.IsLetterOrDigit(key[0]))
            return (uint)char.ToUpper(key[0]);

        return key.ToUpperInvariant() switch
        {
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "SPACE" => 0x20, "ENTER" => 0x0D, "TAB" => 0x09,
            "ESCAPE" or "ESC" => 0x1B,
            "INSERT" => 0x2D, "DELETE" => 0x2E,
            "HOME" => 0x24, "END" => 0x23,
            "PAGEUP" => 0x21, "PAGEDOWN" => 0x22,
            "UP" => 0x26, "DOWN" => 0x28, "LEFT" => 0x25, "RIGHT" => 0x27,
            "NUMPAD0" => 0x60, "NUMPAD1" => 0x61, "NUMPAD2" => 0x62,
            "NUMPAD3" => 0x63, "NUMPAD4" => 0x64, "NUMPAD5" => 0x65,
            "NUMPAD6" => 0x66, "NUMPAD7" => 0x67, "NUMPAD8" => 0x68,
            "NUMPAD9" => 0x69,
            _ => 0
        };
    }

    private static string VirtualKeyToString(uint vk)
    {
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString(); // 0-9
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString(); // A-Z

        return vk switch
        {
            0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
            0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
            0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
            0x20 => "Space", 0x0D => "Enter", 0x09 => "Tab", 0x1B => "Esc",
            0x2D => "Insert", 0x2E => "Delete",
            0x24 => "Home", 0x23 => "End",
            0x21 => "PageUp", 0x22 => "PageDown",
            0x26 => "Up", 0x28 => "Down", 0x25 => "Left", 0x27 => "Right",
            _ => $"0x{vk:X2}"
        };
    }
}
