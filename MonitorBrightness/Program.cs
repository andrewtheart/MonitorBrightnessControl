using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace MonitorBrightness;

public static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [STAThread]
    static void Main(string[] args)
    {
        // Handle CLI commands without launching GUI
        if (CliHandler.TryHandle(args))
        {
            Console.WriteLine();
            Console.Out.Flush();
            return;
        }

        // Parse GUI launch flags (--position, --display)
        ParseGuiFlags(args);

        // This is a console-subsystem executable so CLI commands behave like a normal terminal app.
        // For GUI mode, detach from the auto-created console before starting WinUI.
        FreeConsole();

        // Launch WinUI GUI
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }

    private static void ParseGuiFlags(string[] args)
    {
        var (position, display) = ParseGuiOverrides(args);
        if (position.HasValue) App.OverridePosition = position.Value;
        if (display.HasValue) App.OverrideDisplay = display.Value;

        int demoCount = ParseDemoCount(args);
        if (demoCount > 0)
            App.DemoMonitors = CreateDemoMonitors(demoCount);
    }

    internal static (WindowPosition? position, int? display) ParseGuiOverrides(string[] args)
    {
        WindowPosition? position = null;
        int? display = null;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant().TrimStart('-', '/');

            if (arg is "position" or "pos" or "p" && i + 1 < args.Length)
            {
                if (Enum.TryParse<WindowPosition>(args[i + 1], ignoreCase: true, out var pos))
                    position = pos;
                i++;
            }
            else if (arg is "display" or "d" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int d))
                    display = d;
                i++;
            }
        }

        return (position, display);
    }

    internal static int ParseDemoCount(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant().TrimStart('-', '/');
            if (arg == "demo" && i + 1 < args.Length && int.TryParse(args[i + 1], out int count))
                return Math.Clamp(count, 1, 20);
        }
        return 0;
    }

    internal static List<MonitorDevice> CreateDemoMonitors(int count)
    {
        var names = new[] { "Dell U2722D", "LG 27GL850", "Samsung Odyssey G7", "ASUS PA278QV", "BenQ PD2700U" };
        var widths = new[] { 2560, 2560, 2560, 2560, 3840 };
        var heights = new[] { 1440, 1440, 1440, 1440, 2160 };
        var monitors = new List<MonitorDevice>(count);

        for (int i = 0; i < count; i++)
        {
            monitors.Add(new MonitorDevice
            {
                Index = i,
                EdidName = names[i % names.Length],
                SupportsBrightness = true,
                MinBrightness = 0,
                MaxBrightness = 100,
                CurrentBrightness = 30 + (i * 15) % 71,
                Width = widths[i % widths.Length],
                Height = heights[i % heights.Length],
                Left = i * widths[i % widths.Length],
            });
        }

        return monitors;
    }
}
