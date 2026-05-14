using System.Runtime.InteropServices;

namespace MonitorBrightness;

/// <summary>
/// Represents a physical monitor with its DDC/CI capabilities.
/// </summary>
public class MonitorDevice
{
    public int Index { get; set; }
    public IntPtr HMonitor { get; set; }
    public IntPtr PhysicalMonitorHandle { get; set; }
    public string Description { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string EdidName { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool SupportsBrightness { get; set; }
    public int MinBrightness { get; set; }
    public int MaxBrightness { get; set; } = 100;
    public int CurrentBrightness { get; set; } = 50;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(EdidName))
                return EdidName;
            if (!string.IsNullOrWhiteSpace(FriendlyName) &&
                !FriendlyName.Equals("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase))
                return FriendlyName;
            if (!string.IsNullOrWhiteSpace(Description))
                return Description;
            return $"Monitor {Index + 1}";
        }
    }

    public string Resolution => $"{Width}x{Height}";
}

public sealed class BrightnessSetResult
{
    private BrightnessSetResult(bool succeeded, string? errorMessage)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }
    public string? ErrorMessage { get; }

    public static BrightnessSetResult Success() => new(true, null);

    public static BrightnessSetResult Failure(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Enumerates monitors using Win32 APIs and provides DDC/CI brightness control.
/// </summary>
public static class MonitorEnumerator
{
    private const byte BrightnessVcpCode = 0x10;

    #region Native Structs

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public char[] szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEXW
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    #endregion

    #region Native Methods

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFOEXW lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevicesW(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("dxva2.dll")]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll")]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll")]
    private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetVCPFeature(IntPtr hMonitor, byte bVCPCode, uint dwNewValue);

    [DllImport("dxva2.dll")]
    private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

    #endregion

    public static List<MonitorDevice> GetMonitors(bool readBrightness = true)
    {
        var monitors = new List<MonitorDevice>();
        var hMonitors = new List<IntPtr>();

        MonitorEnumProc enumProc = (IntPtr hMon, IntPtr hdc, ref RECT rect, IntPtr lparam) =>
        {
            hMonitors.Add(hMon);
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, enumProc, IntPtr.Zero);

        for (int idx = 0; idx < hMonitors.Count; idx++)
        {
            var hMon = hMonitors[idx];

            // Get monitor info (rect + device name)
            var monInfo = new MONITORINFOEXW { cbSize = Marshal.SizeOf<MONITORINFOEXW>() };
            if (!GetMonitorInfoW(hMon, ref monInfo))
                continue;

            var deviceName = monInfo.szDevice;
            int left = monInfo.rcMonitor.Left;
            int top = monInfo.rcMonitor.Top;
            int width = monInfo.rcMonitor.Right - monInfo.rcMonitor.Left;
            int height = monInfo.rcMonitor.Bottom - monInfo.rcMonitor.Top;

            // Get friendly name via EnumDisplayDevices
            var (friendlyName, deviceId) = GetFriendlyName(deviceName);
            var edidName = GetEdidMonitorName(deviceId);

            // Get physical monitors for DDC/CI
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint numPhysical) || numPhysical == 0)
            {
                monitors.Add(CreateDisplayOnlyMonitor());
                continue;
            }

            var physicalMonitors = Enumerable
                .Range(0, (int)numPhysical)
                .Select(_ => new PHYSICAL_MONITOR { szPhysicalMonitorDescription = new char[128] })
                .ToArray();
            if (!GetPhysicalMonitorsFromHMONITOR(hMon, numPhysical, physicalMonitors))
            {
                ReleasePhysicalMonitorArray(physicalMonitors);
                monitors.Add(CreateDisplayOnlyMonitor());
                continue;
            }

            bool addedPhysicalMonitor = false;
            foreach (var pm in physicalMonitors)
            {
                if (pm.hPhysicalMonitor == IntPtr.Zero)
                    continue;

                var device = new MonitorDevice
                {
                    Index = idx,
                    HMonitor = hMon,
                    PhysicalMonitorHandle = pm.hPhysicalMonitor,
                    Description = GetPhysicalMonitorDescription(pm),
                    FriendlyName = friendlyName,
                    EdidName = edidName,
                    DeviceName = deviceName,
                    DeviceId = deviceId,
                    Left = left,
                    Top = top,
                    Width = width,
                    Height = height,
                };

                if (!readBrightness)
                {
                    device.SupportsBrightness = true;
                }
                else if (GetMonitorBrightness(pm.hPhysicalMonitor, out uint minB, out uint curB, out uint maxB))
                {
                    device.SupportsBrightness = true;
                    device.MinBrightness = (int)minB;
                    device.CurrentBrightness = (int)curB;
                    device.MaxBrightness = (int)maxB;
                }

                monitors.Add(device);
                addedPhysicalMonitor = true;
            }

            if (!addedPhysicalMonitor)
                monitors.Add(CreateDisplayOnlyMonitor());

            MonitorDevice CreateDisplayOnlyMonitor()
            {
                return new MonitorDevice
                {
                    Index = idx,
                    HMonitor = hMon,
                    PhysicalMonitorHandle = IntPtr.Zero,
                    FriendlyName = friendlyName,
                    EdidName = edidName,
                    DeviceName = deviceName,
                    DeviceId = deviceId,
                    Left = left,
                    Top = top,
                    Width = width,
                    Height = height,
                };
            }
        }

        return monitors;
    }

    public static void ReleaseMonitors(IEnumerable<MonitorDevice> monitors)
    {
        var releasedHandles = new HashSet<IntPtr>();
        foreach (var monitor in monitors)
        {
            if (monitor.PhysicalMonitorHandle == IntPtr.Zero ||
                !releasedHandles.Add(monitor.PhysicalMonitorHandle))
            {
                continue;
            }

            if (!DestroyPhysicalMonitor(monitor.PhysicalMonitorHandle))
                AppLogger.Warn($"Failed to release physical monitor handle for {monitor.DisplayName}");

            monitor.PhysicalMonitorHandle = IntPtr.Zero;
        }
    }

    public static bool SetBrightness(IntPtr physicalMonitorHandle, int brightness)
    {
        return TrySetBrightness(physicalMonitorHandle, brightness).Succeeded;
    }

    public static BrightnessSetResult TrySetBrightness(IntPtr physicalMonitorHandle, int brightness)
    {
        if (physicalMonitorHandle == IntPtr.Zero)
            return BrightnessSetResult.Failure("invalid monitor handle");

        uint brightnessValue = (uint)Math.Clamp(brightness, 0, 100);
        if (SetMonitorBrightness(physicalMonitorHandle, brightnessValue))
            return BrightnessSetResult.Success();

        int highLevelError = Marshal.GetLastWin32Error();
        Thread.Sleep(50);

        if (SetVCPFeature(physicalMonitorHandle, BrightnessVcpCode, brightnessValue))
            return BrightnessSetResult.Success();

        int vcpError = Marshal.GetLastWin32Error();
        var errorMessage = $"SetMonitorBrightness error {highLevelError}, SetVCPFeature error {vcpError}";
        AppLogger.Warn($"Failed to set monitor brightness to {brightnessValue}% ({errorMessage})");
        return BrightnessSetResult.Failure(errorMessage);
    }

    private static void ReleasePhysicalMonitorArray(IEnumerable<PHYSICAL_MONITOR> physicalMonitors)
    {
        var releasedHandles = new HashSet<IntPtr>();
        foreach (var physicalMonitor in physicalMonitors)
        {
            if (physicalMonitor.hPhysicalMonitor == IntPtr.Zero ||
                !releasedHandles.Add(physicalMonitor.hPhysicalMonitor))
            {
                continue;
            }

            if (!DestroyPhysicalMonitor(physicalMonitor.hPhysicalMonitor))
                AppLogger.Warn("Failed to release physical monitor handle after enumeration failure");
        }
    }

    private static string GetPhysicalMonitorDescription(PHYSICAL_MONITOR physicalMonitor)
    {
        return physicalMonitor.szPhysicalMonitorDescription is null
            ? ""
            : new string(physicalMonitor.szPhysicalMonitorDescription).TrimEnd('\0').Trim();
    }

    private static (string friendlyName, string deviceId) GetFriendlyName(string adapterDeviceName)
    {
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        if (EnumDisplayDevicesW(adapterDeviceName, 0, ref dd, 0))
        {
            return (dd.DeviceString ?? "", dd.DeviceID ?? "");
        }
        return ("", "");
    }

    private static string GetEdidMonitorName(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return "";

        try
        {
            // deviceId format: MONITOR\<monitorId>\{guid}
            var parts = deviceId.Split('\\');
            if (parts.Length < 2)
                return "";

            var monitorId = parts[1]; // e.g., "DELA0EC"

            // Search registry for EDID
            using var displayKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Enum\DISPLAY");
            if (displayKey == null) return "";

            foreach (var subName in displayKey.GetSubKeyNames())
            {
                if (!subName.Contains(monitorId, StringComparison.OrdinalIgnoreCase))
                    continue;

                using var sub = displayKey.OpenSubKey(subName);
                if (sub == null) continue;

                foreach (var instanceName in sub.GetSubKeyNames())
                {
                    using var paramsKey = sub.OpenSubKey($@"{instanceName}\Device Parameters");
                    if (paramsKey == null) continue;

                    if (paramsKey.GetValue("EDID") is byte[] edid)
                    {
                        var name = ParseEdidName(edid);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn("Unable to read monitor EDID from registry", ex);
        }

        return "";
    }

    private static string ParseEdidName(byte[] edid)
    {
        if (edid.Length < 128)
            return "";

        // EDID descriptor blocks at offset 54, each 18 bytes
        for (int i = 0; i < 4; i++)
        {
            int offset = 54 + i * 18;
            if (offset + 18 > edid.Length)
                break;

            // Monitor name descriptor: bytes 0-2 = 0, byte 3 = 0xFC
            if (edid[offset] == 0 && edid[offset + 1] == 0 &&
                edid[offset + 2] == 0 && edid[offset + 3] == 0xFC)
            {
                var nameBytes = edid[(offset + 5)..(offset + 18)];
                var name = "";
                foreach (var b in nameBytes)
                {
                    if (b == 0x0A || b == 0x00) break;
                    name += (char)b;
                }
                return name.Trim();
            }
        }
        return "";
    }
}
