using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using ClaudeWatcher.Services;
using ClaudeWatcher.ViewModels;
using H.NotifyIcon;

namespace ClaudeWatcher;

public partial class App : Application
{
    private Mutex? _mutex;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;
    private HookServer? _hookServer;
    private SessionManager? _sessionManager;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance check
        _mutex = new Mutex(true, "ClaudeWatcher_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            BringExistingInstanceToFront();
            Shutdown();
            return;
        }

        // Initialize services
        _sessionManager = new SessionManager();
        var focusService = new TerminalFocusService();
        var recentSessions = new RecentSessionsService();

        // Start HTTP server
        _hookServer = new HookServer(_sessionManager);
        await _hookServer.StartAsync();

        // Create ViewModel
        _mainViewModel = new MainViewModel(_sessionManager, focusService, recentSessions);

        // Create main window (starts hidden)
        _mainWindow = new MainWindow
        {
            DataContext = _mainViewModel,
            Visibility = Visibility.Hidden
        };

        // Show/hide window when sessions change
        _mainViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.HasSessions))
            {
                if (_mainViewModel.HasSessions)
                    ShowMainWindow();
                else
                    _mainWindow.Visibility = Visibility.Hidden;
            }
        };

        // Setup tray icon
        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = $"ClaudeWatcher (port {_hookServer?.Port ?? 22322})",
            Visibility = Visibility.Visible
        };

        _trayIcon.Icon = CreateDefaultIcon();
        _trayIcon.TrayLeftMouseUp += (_, _) => ToggleMainWindow();
    }

    private static System.Drawing.Icon CreateDefaultIcon()
    {
        const int size = 16;
        var pixels = new byte[size * size * 4];
        int cx = size / 2, cy = size / 2;
        double r = 7.0;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                int idx = (y * size + x) * 4;
                if (dist <= r)
                {
                    pixels[idx + 0] = 0xF3; // B
                    pixels[idx + 1] = 0x96; // G
                    pixels[idx + 2] = 0x21; // R
                    pixels[idx + 3] = 0xFF; // A
                }
            }
        }

        using var ms = new System.IO.MemoryStream();
        using var bw = new System.IO.BinaryWriter(ms);

        bw.Write((short)0);
        bw.Write((short)1);
        bw.Write((short)1);

        int bmpInfoSize = 40;
        int imageSize = bmpInfoSize + pixels.Length;
        int dataOffset = 6 + 16;

        bw.Write((byte)size);
        bw.Write((byte)size);
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((short)1);
        bw.Write((short)32);
        bw.Write(imageSize);
        bw.Write(dataOffset);

        bw.Write(bmpInfoSize);
        bw.Write(size);
        bw.Write(size * 2);
        bw.Write((short)1);
        bw.Write((short)32);
        bw.Write(0);
        bw.Write(pixels.Length);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);

        for (int y = size - 1; y >= 0; y--)
            bw.Write(pixels, y * size * 4, size * 4);

        bw.Flush();
        ms.Position = 0;
        return new System.Drawing.Icon(ms);
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow == null) return;
        if (_mainWindow.Visibility == Visibility.Visible)
            _mainWindow.Visibility = Visibility.Hidden;
        else
            ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Visibility = Visibility.Visible;
        _mainWindow.Activate();
    }

    public MainViewModel? ViewModel => _mainViewModel;

    private static void BringExistingInstanceToFront()
    {
        var currentProcess = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName(currentProcess.ProcessName);
        foreach (var process in processes)
        {
            if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
            {
                ShowWindow(process.MainWindowHandle, SW_RESTORE);
                SetForegroundWindow(process.MainWindowHandle);
                break;
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hookServer?.Dispose();
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
