using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using ClaudeWatcher.Helpers;

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

    private const int SW_RESTORE = 9;

    public bool FocusSession(string? tabTitle, int shellPid = 0, string? cwd = null)
    {
        Log.Info($"FocusSession: tabTitle='{tabTitle}', shellPid={shellPid}, cwd='{cwd}'");

        // Primary: process tree — walk up from shell PID to find the terminal window
        if (shellPid > 0 && TryFocusViaProcessTree(shellPid, cwd))
            return true;

        // Fallback: UIA scan of all WT windows, match by title or CWD
        if (TryFocusWindowsTerminalTab(tabTitle, cwd))
            return true;

        Log.Info("Focus failed: no match via process tree or UIA");
        return false;
    }

    private bool TryFocusViaProcessTree(int shellPid, string? cwd)
    {
        try
        {
            IntPtr hwnd = ProcessTreeHelper.FindTerminalWindowHandle(shellPid);
            if (hwnd == IntPtr.Zero)
            {
                Log.Info($"Process tree from PID {shellPid}: no terminal window found");
                return false;
            }

            Log.Info($"Process tree from PID {shellPid}: found window {hwnd}");
            ForceForeground(hwnd);

            // If the window has multiple tabs, try to select the right one
            TrySelectTab(hwnd, cwd);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Process tree focus failed for PID {shellPid}", ex);
            return false;
        }
    }

    private void TrySelectTab(IntPtr hwnd, string? cwd)
    {
        try
        {
            var window = AutomationElement.FromHandle(hwnd);
            var tabs = window.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

            if (tabs.Count <= 1)
            {
                Log.Info($"Window has {tabs.Count} tab(s), no selection needed");
                return;
            }

            Log.Info($"Window has {tabs.Count} tabs, attempting match by CWD");

            string dirName = "";
            if (!string.IsNullOrEmpty(cwd))
                dirName = Path.GetFileName(cwd.TrimEnd('\\', '/')) ?? "";

            if (string.IsNullOrEmpty(dirName)) return;

            foreach (AutomationElement tab in tabs)
            {
                try
                {
                    string tabName = tab.Current.Name ?? "";
                    if (tabName.Contains(dirName, StringComparison.OrdinalIgnoreCase))
                    {
                        ActivateTab(tab);
                        Log.Info($"Selected tab '{tabName}' matching CWD component '{dirName}'");
                        return;
                    }
                }
                catch { }
            }

            Log.Info($"No tab matched CWD '{dirName}', window is focused but tab not selected");
        }
        catch (Exception ex)
        {
            Log.Error("Tab selection within window failed", ex);
        }
    }

    private bool TryFocusWindowsTerminalTab(string? tabTitle, string? cwd)
    {
        try
        {
            var desktop = AutomationElement.RootElement;
            var wtWindows = desktop.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ClassNameProperty,
                    "CASCADIA_HOSTING_WINDOW_CLASS"));

            Log.Info($"UIA fallback: {wtWindows.Count} WT window(s)");

            // Build search terms
            string dirName = "";
            if (!string.IsNullOrEmpty(cwd))
                dirName = Path.GetFileName(cwd.TrimEnd('\\', '/')) ?? "";

            foreach (AutomationElement wtWin in wtWindows)
            {
                var tabs = wtWin.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

                foreach (AutomationElement tab in tabs)
                {
                    try
                    {
                        string tabName = tab.Current.Name ?? "";

                        // Match by explicit title
                        if (!string.IsNullOrEmpty(tabTitle) &&
                            tabName.Contains(tabTitle, StringComparison.OrdinalIgnoreCase))
                        {
                            ActivateTab(tab);
                            ForceForeground(GetWindowHandle(wtWin));
                            Log.Info($"UIA matched '{tabName}' by title '{tabTitle}'");
                            return true;
                        }

                        // Match by CWD directory name
                        if (!string.IsNullOrEmpty(dirName) &&
                            tabName.Contains(dirName, StringComparison.OrdinalIgnoreCase))
                        {
                            ActivateTab(tab);
                            ForceForeground(GetWindowHandle(wtWin));
                            Log.Info($"UIA matched '{tabName}' by CWD '{dirName}'");
                            return true;
                        }
                    }
                    catch { }
                }
            }

            Log.Info("UIA fallback: no matching tab found");
        }
        catch (Exception ex)
        {
            Log.Error("UIA tab scan failed", ex);
        }

        return false;
    }

    private static void ActivateTab(AutomationElement tab)
    {
        if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? selPat))
            ((SelectionItemPattern)selPat).Select();
        else if (tab.TryGetCurrentPattern(InvokePattern.Pattern, out object? invPat))
            ((InvokePattern)invPat).Invoke();
    }

    private static IntPtr GetWindowHandle(AutomationElement element)
    {
        return new IntPtr(
            (int)element.GetCurrentPropertyValue(
                AutomationElement.NativeWindowHandleProperty));
    }

    private void ForceForeground(IntPtr hwnd)
    {
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
