using System.IO;

namespace ClaudeWatcher.Services;

public static class Log
{
    private static readonly string LogDir;
    private static readonly string LogFile;
    private static readonly object Lock = new();

    static Log()
    {
        LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudeWatcher");
        Directory.CreateDirectory(LogDir);
        LogFile = Path.Combine(LogDir, "log.txt");
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        var text = ex != null ? $"{message}: {ex}" : message;
        Write("ERROR", text);
    }

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
            lock (Lock)
            {
                File.AppendAllText(LogFile, line);

                // Keep log file under 1MB — trim old entries
                var fi = new FileInfo(LogFile);
                if (fi.Length > 1_000_000)
                {
                    var lines = File.ReadAllLines(LogFile);
                    var keep = lines[^500..]; // keep last 500 lines
                    File.WriteAllLines(LogFile, keep);
                }
            }
        }
        catch
        {
            // Last resort — never crash from logging
        }
    }

    public static string FilePath => LogFile;
}
