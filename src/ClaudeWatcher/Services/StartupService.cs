using Microsoft.Win32;

namespace ClaudeWatcher.Services;

public static class StartupService
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ClaudeWatcher";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
        return key?.GetValue(AppName) != null;
    }

    public static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        if (key == null) return;

        if (enable)
        {
            string exePath = Environment.ProcessPath ?? "";
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}
