using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using ClaudeWatcher.Models;
using ClaudeWatcher.ViewModels;

namespace ClaudeWatcher.Views;

public partial class SessionCard : UserControl
{
    public SessionCard()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SessionCardViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is SessionCardViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateStatusIndicator(newVm.Status);
            UpdateSubagentSpinners(newVm.ActiveSubagentCount);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not SessionCardViewModel vm) return;

        if (e.PropertyName == nameof(SessionCardViewModel.Status))
            Dispatcher.Invoke(() => UpdateStatusIndicator(vm.Status));
        else if (e.PropertyName == nameof(SessionCardViewModel.ActiveSubagentCount))
            Dispatcher.Invoke(() => UpdateSubagentSpinners(vm.ActiveSubagentCount));
    }

    private void UpdateStatusIndicator(SessionStatus status)
    {
        // Hide all
        SpinnerPath.Visibility = Visibility.Collapsed;
        ToolIcon.Visibility = Visibility.Collapsed;
        PermissionIcon.Visibility = Visibility.Collapsed;
        WaitingDot.Visibility = Visibility.Collapsed;
        ErrorDot.Visibility = Visibility.Collapsed;
        IdleDot.Visibility = Visibility.Collapsed;

        // Stop spinner animation
        SpinnerRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        WaitingDot.BeginAnimation(OpacityProperty, null);

        switch (status)
        {
            case SessionStatus.Working:
                SpinnerPath.Visibility = Visibility.Visible;
                var spinAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                SpinnerRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spinAnim);
                break;

            case SessionStatus.Tool:
                ToolIcon.Visibility = Visibility.Visible;
                break;

            case SessionStatus.Permission:
                PermissionIcon.Visibility = Visibility.Visible;
                break;

            case SessionStatus.Waiting:
                WaitingDot.Visibility = Visibility.Visible;
                var yellowFlash = new DoubleAnimation(1.0, 0.3, TimeSpan.FromSeconds(0.8))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                WaitingDot.BeginAnimation(OpacityProperty, yellowFlash);
                break;

            case SessionStatus.Error:
                ErrorDot.Visibility = Visibility.Visible;
                break;

            case SessionStatus.Idle:
                IdleDot.Visibility = Visibility.Visible;
                break;
        }
    }

    private void UpdateSubagentSpinners(int count)
    {
        if (count > 0)
        {
            SubagentSpinners.Visibility = Visibility.Visible;
            // Create a list of dummy items for the ItemsControl to render spinners
            SubagentSpinners.ItemsSource = Enumerable.Range(0, count).ToList();
        }
        else
        {
            SubagentSpinners.Visibility = Visibility.Collapsed;
            SubagentSpinners.ItemsSource = null;
        }
    }

    private void Card_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is SessionCardViewModel vm)
        {
            vm.OnCardClicked();
        }
    }
}
