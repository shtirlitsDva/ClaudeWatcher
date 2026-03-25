using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ClaudeWatcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Taskbar "Close window" sends WM_CLOSE → triggers Closing.
        // With OnExplicitShutdown, the window would close but the process
        // would keep running. Shut down the whole app instead.
        Application.Current.Shutdown();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_RightClick(object sender, MouseButtonEventArgs e)
    {
        // Context menu is handled automatically by the Border.ContextMenu
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        Visibility = Visibility.Hidden;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
