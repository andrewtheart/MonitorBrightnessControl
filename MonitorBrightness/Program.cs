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
}
