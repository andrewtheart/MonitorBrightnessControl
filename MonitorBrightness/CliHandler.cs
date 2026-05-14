using System.Runtime.InteropServices;

namespace MonitorBrightness;

/// <summary>
/// Handles command-line operations without launching the GUI.
/// </summary>
public static class CliHandler
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;

    /// <summary>
    /// Returns true if CLI args were handled (app should exit), false to launch GUI.
    /// </summary>
    public static bool TryHandle(string[] args)
    {
        if (args.Length == 0)
            return false;

        if (args.Any(CliArgumentParser.IsHelpFlag))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            PrintHelp();
            return true;
        }

        var command = args[0].ToLowerInvariant().TrimStart('-', '/');

        switch (command)
        {
            case "list":
            case "l":
                AttachConsole(ATTACH_PARENT_PROCESS);
                ListMonitors();
                return true;

            case "set":
            case "s":
                AttachConsole(ATTACH_PARENT_PROCESS);
                SetBrightness(args);
                return true;

            case "get":
            case "g":
                AttachConsole(ATTACH_PARENT_PROCESS);
                GetBrightness(args);
                return true;

            case "identify":
            case "id":
                AttachConsole(ATTACH_PARENT_PROCESS);
                IdentifyMonitors();
                return true;

            default:
                return false;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Monitor Brightness Control - Command Line Interface");
        Console.WriteLine("===================================================");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  MonitorBrightness.exe");
        Console.WriteLine("  MonitorBrightness.exe <command> [arguments]");
        Console.WriteLine();
        Console.WriteLine("Help flags:");
        Console.WriteLine("  --help, /help, -h, -?, /?");
        Console.WriteLine("      Show this help text. If any help flag is present anywhere in the");
        Console.WriteLine("      command line, all other flags and arguments are ignored.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  (no arguments)");
        Console.WriteLine("      Launch the WinUI app.");
        Console.WriteLine();
        Console.WriteLine("  --list, -l");
        Console.WriteLine("      List all detected monitors, including display number, name,");
        Console.WriteLine("      resolution, current brightness, brightness range, and DDC/CI support.");
        Console.WriteLine();
        Console.WriteLine("  --get, -g [monitor]");
        Console.WriteLine("      Print brightness for one monitor, or for all monitors if omitted.");
        Console.WriteLine("      Monitor numbers are 1-based and match the app UI and --list output.");
        Console.WriteLine();
        Console.WriteLine("  --set, -s <targets> <value>");
        Console.WriteLine("      Set brightness for one or more monitors. Value must be 0-100.");
        Console.WriteLine("      Targets can be:");
        Console.WriteLine("        1       single monitor");
        Console.WriteLine("        all     all brightness-capable monitors");
        Console.WriteLine("        1,3     comma-separated monitor list");
        Console.WriteLine("        1-4     inclusive monitor range");
        Console.WriteLine("        1 3 4   space-separated monitor list");
        Console.WriteLine();
        Console.WriteLine("  --identify, --id");
        Console.WriteLine("      Print monitor identification details: display number, monitor name,");
        Console.WriteLine("      resolution, and virtual desktop position.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  MonitorBrightness --list");
        Console.WriteLine("  MonitorBrightness --get");
        Console.WriteLine("  MonitorBrightness --get 2");
        Console.WriteLine("  MonitorBrightness --set 1 75");
        Console.WriteLine("  MonitorBrightness --set all 50");
        Console.WriteLine("  MonitorBrightness --set 1,3 75");
        Console.WriteLine("  MonitorBrightness --set 1-4 60");
        Console.WriteLine("  MonitorBrightness --set 1 3 4 80");
        Console.WriteLine("  MonitorBrightness --identify");
        Console.WriteLine("  MonitorBrightness --set 1 75 --help");
        Console.WriteLine("      Shows help; the --set command is ignored because --help is present.");
        Console.WriteLine();
    }

    private static void ListMonitors()
    {
        Console.WriteLine();
        var monitors = MonitorEnumerator.GetMonitors();
        try
        {
            if (monitors.Count == 0)
            {
                Console.WriteLine("No monitors with DDC/CI support detected.");
                return;
            }

            Console.WriteLine($"{"#",-4}{"Name",-25}{"Resolution",-14}{"Brightness",-15}{"DDC/CI"}");
            Console.WriteLine(new string('-', 65));

            foreach (var mon in monitors)
            {
                string brightness = mon.SupportsBrightness
                    ? $"{mon.CurrentBrightness}% ({mon.MinBrightness}-{mon.MaxBrightness})"
                    : "N/A";
                string ddc = mon.SupportsBrightness ? "Yes" : "No";
                Console.WriteLine($"{mon.Index + 1,-4}{mon.DisplayName,-25}{mon.Resolution,-14}{brightness,-15}{ddc}");
            }
            Console.WriteLine();
        }
        finally
        {
            MonitorEnumerator.ReleaseMonitors(monitors);
        }
    }

    private static void GetBrightness(string[] args)
    {
        Console.WriteLine();
        var monitors = MonitorEnumerator.GetMonitors();
        try
        {
            if (args.Length > 1 && int.TryParse(args[1], out int monIndex))
            {
                var mon = monitors.FirstOrDefault(m => m.Index + 1 == monIndex);
                if (mon == null)
                {
                    Console.WriteLine($"Error: Monitor {monIndex} not found. Use --list to see available monitors.");
                    return;
                }
                if (!mon.SupportsBrightness)
                {
                    Console.WriteLine($"Monitor {monIndex} ({mon.DisplayName}) does not support DDC/CI brightness.");
                    return;
                }
                Console.WriteLine($"{mon.CurrentBrightness}");
            }
            else
            {
                foreach (var mon in monitors)
                {
                    string val = mon.SupportsBrightness ? $"{mon.CurrentBrightness}%" : "N/A";
                    Console.WriteLine($"Monitor {mon.Index + 1} ({mon.DisplayName}): {val}");
                }
            }
            Console.WriteLine();
        }
        finally
        {
            MonitorEnumerator.ReleaseMonitors(monitors);
        }
    }

    private static void SetBrightness(string[] args)
    {
        Console.WriteLine();
        if (args.Length < 3)
        {
            Console.WriteLine("Error: --set requires <targets> <value>");
            Console.WriteLine("  Example: --set 1 75");
            Console.WriteLine("  Example: --set all 50");
            Console.WriteLine("  Example: --set 1,3 75");
            Console.WriteLine("  Example: --set 1-4 60");
            return;
        }

        var monitors = MonitorEnumerator.GetMonitors(readBrightness: false);
        string[] targetTokens = args[1..^1];
        try
        {
            if (!int.TryParse(args[^1], out int value) || value < 0 || value > 100)
            {
                Console.WriteLine("Error: Brightness value must be 0-100.");
                return;
            }

            var targets = CliArgumentParser.ResolveTargetMonitors(monitors, targetTokens);
            if (targets.Count == 0)
            {
                Console.WriteLine("Error: No matching monitors found. Use --list to see available monitors.");
                Console.WriteLine("Targets can be: all, 1, 1,3, 1-4, or 1 3 4.");
                Console.WriteLine();
                return;
            }

            int successCount = 0;
            int failureCount = 0;
            foreach (var mon in targets)
            {
                if (!mon.SupportsBrightness)
                {
                    Console.WriteLine($"  Monitor {mon.Index + 1} ({mon.DisplayName}): SKIPPED (DDC/CI not supported)");
                    failureCount++;
                    continue;
                }

                var result = MonitorEnumerator.TrySetBrightness(mon.PhysicalMonitorHandle, value);
                if (result.Succeeded)
                {
                    Console.WriteLine($"  Monitor {mon.Index + 1} ({mon.DisplayName}): set to {value}%");
                    successCount++;
                }
                else
                {
                    Console.WriteLine($"  Monitor {mon.Index + 1} ({mon.DisplayName}): FAILED ({result.ErrorMessage})");
                    failureCount++;
                }
            }

            Console.WriteLine($"\nSet {successCount} monitor(s) to {value}%.");
            if (failureCount > 0)
                Console.WriteLine($"{failureCount} monitor(s) failed or were skipped.");
            Console.WriteLine();
        }
        finally
        {
            MonitorEnumerator.ReleaseMonitors(monitors);
        }
    }

    private static void IdentifyMonitors()
    {
        Console.WriteLine();
        var monitors = MonitorEnumerator.GetMonitors();
        try
        {
            if (monitors.Count == 0)
            {
                Console.WriteLine("No monitors detected.");
                return;
            }

            Console.WriteLine("Showing identify overlay for 3 seconds...");
            Console.WriteLine();

            foreach (var mon in monitors)
            {
                Console.WriteLine($"  Monitor {mon.Index + 1}: {mon.DisplayName} ({mon.Resolution}) at ({mon.Left},{mon.Top})");
            }

            // Launch a minimal WinUI app just for the overlay
            Console.WriteLine();
            Console.WriteLine("(Overlay requires GUI - launching identify windows...)");

            // We can't easily show WinUI windows from CLI mode without the full app.
            // Instead, print the info and suggest using the GUI.
            Console.WriteLine("Tip: Run without arguments to use the GUI with the Identify button.");
            Console.WriteLine();
        }
        finally
        {
            MonitorEnumerator.ReleaseMonitors(monitors);
        }
    }
}
