using System.Runtime.InteropServices;

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

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

    public static IntPtr FindTerminalWindowHandle(int shellPid)
    {
        int pid = shellPid;
        for (int i = 0; i < 5; i++) // Walk up max 5 levels
        {
            int parentPid = GetParentProcessId(pid);
            if (parentPid <= 0) break;

            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(parentPid);
                if (proc.MainWindowHandle != IntPtr.Zero)
                    return proc.MainWindowHandle;
            }
            catch { }

            pid = parentPid;
        }
        return IntPtr.Zero;
    }
}
