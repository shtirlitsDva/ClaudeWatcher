using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using ClaudeWatcher.ViewModels;
using H.NotifyIcon;

namespace ClaudeWatcher;

public partial class App : Application
{
    private Mutex? _mutex;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    protected override void OnStartup(StartupEventArgs e)
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

        _mainViewModel = new MainViewModel();

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
            ToolTipText = "ClaudeWatcher",
            Visibility = Visibility.Visible
        };

        // Generate a simple icon programmatically
        _trayIcon.Icon = CreateDefaultIcon();

        // Left-click toggles window
        _trayIcon.TrayLeftMouseUp += (_, _) => ToggleMainWindow();
    }

    private static System.Drawing.Icon CreateDefaultIcon()
    {
        // Build a minimal 16x16 32-bit .ico in memory
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
                    // BGRA: blue circle (#2196F3)
                    pixels[idx + 0] = 0xF3; // B
                    pixels[idx + 1] = 0x96; // G
                    pixels[idx + 2] = 0x21; // R
                    pixels[idx + 3] = 0xFF; // A
                }
            }
        }

        // ICO file format: header + directory entry + BMP info header + pixels
        using var ms = new System.IO.MemoryStream();
        using var bw = new System.IO.BinaryWriter(ms);

        // ICO header
        bw.Write((short)0);     // reserved
        bw.Write((short)1);     // type: icon
        bw.Write((short)1);     // count

        // Directory entry
        int bmpInfoSize = 40;
        int imageSize = bmpInfoSize + pixels.Length;
        int dataOffset = 6 + 16; // header(6) + one entry(16)

        bw.Write((byte)size);   // width
        bw.Write((byte)size);   // height
        bw.Write((byte)0);     // color palette
        bw.Write((byte)0);     // reserved
        bw.Write((short)1);     // color planes
        bw.Write((short)32);    // bits per pixel
        bw.Write(imageSize);    // image data size
        bw.Write(dataOffset);   // offset to data

        // BITMAPINFOHEADER
        bw.Write(bmpInfoSize);  // biSize
        bw.Write(size);         // biWidth
        bw.Write(size * 2);     // biHeight (doubled for ICO)
        bw.Write((short)1);     // biPlanes
        bw.Write((short)32);    // biBitCount
        bw.Write(0);            // biCompression
        bw.Write(pixels.Length); // biSizeImage
        bw.Write(0);            // biXPelsPerMeter
        bw.Write(0);            // biYPelsPerMeter
        bw.Write(0);            // biClrUsed
        bw.Write(0);            // biClrImportant

        // Pixel data (bottom-up for BMP)
        for (int y = size - 1; y >= 0; y--)
        {
            bw.Write(pixels, y * size * 4, size * 4);
        }

        bw.Flush();
        ms.Position = 0;
        return new System.Drawing.Icon(ms);
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.Visibility == Visibility.Visible)
        {
            _mainWindow.Visibility = Visibility.Hidden;
        }
        else
        {
            ShowMainWindow();
        }
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
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
