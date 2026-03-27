using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ClaudeWatcher.Services;

public class TerminalFocusService
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    private static readonly string WezTermExe = FindWezTerm();

    private static string FindWezTerm()
    {
        var progFiles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WezTerm", "wezterm.exe");
        return File.Exists(progFiles) ? progFiles : "wezterm";
    }

    public bool FocusSession(string? cwd)
    {
        Log.Info($"FocusSession: cwd='{cwd}'");

        if (string.IsNullOrEmpty(cwd))
            return false;

        if (TryFocusWezTermPane(cwd))
            return true;

        Log.Info("Focus failed: no matching WezTerm pane");
        return false;
    }

    private bool TryFocusWezTermPane(string cwd)
    {
        try
        {
            // Query WezTerm for all panes
            var json = RunProcess(WezTermExe, "cli list --format json");
            if (string.IsNullOrEmpty(json))
            {
                Log.Info("wezterm cli list returned empty");
                return false;
            }

            var panes = JsonSerializer.Deserialize<JsonElement>(json);
            if (panes.ValueKind != JsonValueKind.Array)
            {
                Log.Info($"Unexpected JSON kind: {panes.ValueKind}");
                return false;
            }

            // Normalize the target CWD for comparison
            string targetCwd = NormalizePath(cwd);
            Log.Info($"Looking for CWD: {targetCwd} among {panes.GetArrayLength()} pane(s)");

            int matchedPaneId = -1;
            foreach (var pane in panes.EnumerateArray())
            {
                var paneCwd = pane.TryGetProperty("cwd", out var cwdProp) ? cwdProp.GetString() ?? "" : "";
                var paneId = pane.TryGetProperty("pane_id", out var idProp) ? idProp.GetInt32() : -1;
                var paneTitle = pane.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";

                string normalizedPaneCwd = NormalizePath(paneCwd);
                Log.Info($"  Pane {paneId}: cwd='{normalizedPaneCwd}' title='{paneTitle}'");

                if (paneId >= 0 && string.Equals(targetCwd, normalizedPaneCwd, StringComparison.OrdinalIgnoreCase))
                {
                    matchedPaneId = paneId;
                    Log.Info($"  -> CWD match! pane_id={paneId}");
                    break;
                }
            }

            if (matchedPaneId < 0)
            {
                Log.Info("No pane matched by CWD");
                return false;
            }

            // Bring WezTerm window to foreground
            BringWezTermToFront();

            // Activate the matched pane
            RunProcess(WezTermExe, $"cli activate-pane --pane-id {matchedPaneId}");
            Log.Info($"Activated pane {matchedPaneId}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("WezTerm pane focus failed", ex);
            return false;
        }
    }

    private static string NormalizePath(string path)
    {
        // Handle file:// URIs that WezTerm may return
        if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            path = path[8..]; // strip "file:///"
        else if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            path = path[7..];

        // URL-decode %20 etc.
        path = Uri.UnescapeDataString(path);

        // Normalize separators and trim trailing slashes
        return path.Replace('/', '\\').TrimEnd('\\');
    }

    private void BringWezTermToFront()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("wezterm-gui"))
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    ForceForeground(proc.MainWindowHandle);
                    Log.Info($"Brought wezterm-gui window to front (PID {proc.Id})");
                    return;
                }
            }
            Log.Info("No wezterm-gui process with window found");
        }
        catch (Exception ex)
        {
            Log.Error("BringWezTermToFront failed", ex);
        }
    }

    private static string RunProcess(string fileName, string arguments)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);
            return output;
        }
        catch (Exception ex)
        {
            Log.Error($"RunProcess '{fileName} {arguments}' failed", ex);
            return "";
        }
    }

    private void ForceForeground(IntPtr hwnd)
    {
        if (IsIconic(hwnd))
            ShowWindow(hwnd, SW_RESTORE);

        uint foreThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint appThread = GetCurrentThreadId();

        if (foreThread != appThread)
        {
            AttachThreadInput(foreThread, appThread, true);
            SetForegroundWindow(hwnd);
            AttachThreadInput(foreThread, appThread, false);
        }
        else
        {
            SetForegroundWindow(hwnd);
        }
    }
}
