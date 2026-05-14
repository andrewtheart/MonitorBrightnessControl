using System.Diagnostics;

namespace MonitorBrightness;

internal static class AppLogger
{
    public static void Warn(string message, Exception? exception = null)
    {
        var logMessage = exception is null
            ? $"[{DateTimeOffset.Now:u}] {message}"
            : $"[{DateTimeOffset.Now:u}] {message}: {exception}";

        Debug.WriteLine($"[MonitorBrightness] {logMessage}");
        WriteLogFile(logMessage);
    }

    private static void WriteLogFile(string message)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MonitorBrightness");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(Path.Combine(logDirectory, "monitor_brightness.log"), message + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MonitorBrightness] Unable to write log file: {ex}");
        }
    }
}
