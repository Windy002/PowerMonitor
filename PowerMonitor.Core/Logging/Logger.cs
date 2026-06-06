namespace PowerMonitor.Core.Logging;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PowerMonitor", "app.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"{DateTime.UtcNow:o} [{level}] {message}{Environment.NewLine}");
        }
        catch { /* silently drop log if we can't write */ }
    }

    public static string LogPathValue => LogPath;
}
