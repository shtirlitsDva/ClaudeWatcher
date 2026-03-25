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
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionCardViewModel.Status) && sender is SessionCardViewModel vm)
        {
            Dispatcher.Invoke(() => UpdateStatusIndicator(vm.Status));
        }
    }

    private void UpdateStatusIndicator(SessionStatus status)
    {
        // Hide all
        SpinnerPath.Visibility = Visibility.Collapsed;
        WaitingDot.Visibility = Visibility.Collapsed;
        ErrorDot.Visibility = Visibility.Collapsed;
        IdleDot.Visibility = Visibility.Collapsed;

        // Stop all animations
        SpinnerPath.BeginAnimation(null, null);
        WaitingDot.BeginAnimation(OpacityProperty, null);
        ErrorDot.BeginAnimation(OpacityProperty, null);

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
                var redFlash = new DoubleAnimation(1.0, 0.3, TimeSpan.FromSeconds(0.5))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                ErrorDot.BeginAnimation(OpacityProperty, redFlash);
                break;

            case SessionStatus.Idle:
                IdleDot.Visibility = Visibility.Visible;
                break;
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
