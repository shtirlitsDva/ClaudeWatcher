using System.Diagnostics;
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

    public bool FocusSession(string? tabTitle)
    {
        if (string.IsNullOrEmpty(tabTitle)) return false;

        // Try Windows Terminal tab matching first
        if (TryFocusWindowsTerminalTab(tabTitle))
            return true;

        return false;
    }

    private bool TryFocusWindowsTerminalTab(string tabTitle)
    {
        try
        {
            var desktop = AutomationElement.RootElement;
            var wtWindows = desktop.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ClassNameProperty,
                    "CASCADIA_HOSTING_WINDOW_CLASS"));

            foreach (AutomationElement wtWin in wtWindows)
            {
                // Search for tab by name — try multiple ControlTypes since WT is undocumented
                AutomationElement? tab = FindTabByName(wtWin, tabTitle);
                if (tab == null) continue;

                // Activate the tab
                if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? selPat))
                    ((SelectionItemPattern)selPat).Select();
                else if (tab.TryGetCurrentPattern(InvokePattern.Pattern, out object? invPat))
                    ((InvokePattern)invPat).Invoke();

                // Bring WT window to front
                IntPtr hwnd = new IntPtr(
                    (int)wtWin.GetCurrentPropertyValue(
                        AutomationElement.NativeWindowHandleProperty));
                ForceForeground(hwnd);
                return true;
            }
        }
        catch
        {
            // UI Automation can throw if window closed during enumeration
        }

        return false;
    }

    private static AutomationElement? FindTabByName(AutomationElement window, string name)
    {
        // Try TabItem first
        var tab = window.FindFirst(TreeScope.Descendants,
            new AndCondition(
                new PropertyCondition(AutomationElement.NameProperty, name),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)));
        if (tab != null) return tab;

        // Try ListItem (some WT versions use this)
        tab = window.FindFirst(TreeScope.Descendants,
            new AndCondition(
                new PropertyCondition(AutomationElement.NameProperty, name),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)));
        if (tab != null) return tab;

        // Last resort: any element with matching name
        tab = window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, name));
        return tab;
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
