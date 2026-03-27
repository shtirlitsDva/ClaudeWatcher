using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeWatcher.Helpers;

public static class ProcessTreeHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    public static int GetParentProcessId(int pid)
    {
        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero) return -1;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry)) return -1;

            do
            {
                if (entry.th32ProcessID == (uint)pid)
                    return (int)entry.th32ParentProcessID;
            } while (Process32Next(snapshot, ref entry));

            return -1;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    /// <summary>
    /// Finds the Windows Terminal window (CASCADIA_HOSTING_WINDOW_CLASS) that owns the
    /// given shell process by checking if any visible WT window's process is an ancestor.
    /// </summary>
    public static IntPtr FindTerminalWindowHandle(int shellPid)
    {
        // Collect ancestor process IDs (walk up max 10 levels)
        var ancestors = new HashSet<int>();
        int pid = shellPid;
        for (int i = 0; i < 10; i++)
        {
            int parent = GetParentProcessId(pid);
            if (parent <= 0) break;
            ancestors.Add(parent);
            pid = parent;
        }

        if (ancestors.Count == 0) return IntPtr.Zero;

        // Find a visible CASCADIA_HOSTING_WINDOW_CLASS window owned by an ancestor process
        IntPtr result = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;

            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            if (sb.ToString() != "CASCADIA_HOSTING_WINDOW_CLASS") return true;

            GetWindowThreadProcessId(hwnd, out uint winPid);
            if (ancestors.Contains((int)winPid))
            {
                result = hwnd;
                return false; // stop enumerating
            }
            return true;
        }, IntPtr.Zero);

        return result;
    }
}
