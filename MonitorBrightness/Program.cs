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
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant().TrimStart('-', '/');

            if (arg is "position" or "pos" or "p" && i + 1 < args.Length)
            {
                if (Enum.TryParse<WindowPosition>(args[i + 1], ignoreCase: true, out var pos))
                    App.OverridePosition = pos;
                i++;
            }
            else if (arg is "display" or "d" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int display))
                    App.OverrideDisplay = display;
                i++;
            }
        }
    }
}
