using System.Windows;
using System.Windows.Input;

namespace ClaudeWatcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
