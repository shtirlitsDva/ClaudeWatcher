using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using ClaudeWatcher.Models;
using ClaudeWatcher.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeWatcher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SessionManager _sessionManager;
    private readonly TerminalFocusService _focusService;
    private readonly RecentSessionsService _recentSessions;
    private readonly DispatcherTimer _cleanupTimer;

    public ObservableCollection<SessionCardViewModel> Sessions { get; } = new();
    public ObservableCollection<RecentSession> RecentSessions => new(_recentSessions.Sessions);

    [ObservableProperty]
    private bool _hasSessions;

    [ObservableProperty]
    private bool _launchOnStartup;

    public MainViewModel(SessionManager sessionManager, TerminalFocusService focusService,
        RecentSessionsService recentSessions)
    {
        _sessionManager = sessionManager;
        _focusService = focusService;
        _recentSessions = recentSessions;
        _launchOnStartup = StartupService.IsEnabled();

        Sessions.CollectionChanged += OnSessionsChanged;

        _sessionManager.SessionAdded += OnSessionAdded;
        _sessionManager.SessionUpdated += OnSessionUpdated;
        _sessionManager.SessionRemoved += OnSessionRemoved;

        // Orphan cleanup every 60 seconds
        _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _cleanupTimer.Tick += (_, _) => _sessionManager.CleanupStale();
        _cleanupTimer.Start();
    }

    private void OnSessionAdded(SessionInfo session)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var card = new SessionCardViewModel();
            card.UpdateFrom(session);
            card.CardClicked = OnCardClicked;
            Sessions.Insert(0, card);
        });
    }

    private void OnSessionUpdated(SessionInfo session)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var card = Sessions.FirstOrDefault(s => s.SessionId == session.SessionId);
            card?.UpdateFrom(session);
        });
    }

    private void OnSessionRemoved(SessionInfo session)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var card = Sessions.FirstOrDefault(s => s.SessionId == session.SessionId);
            if (card != null)
            {
                card.StopTimer();
                Sessions.Remove(card);
            }
            _recentSessions.AddOrUpdate(session);
            OnPropertyChanged(nameof(RecentSessions));
        });
    }

    private void OnCardClicked(SessionCardViewModel card)
    {
        _focusService.FocusSession(card.TabTitle);
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasSessions = Sessions.Count > 0;
    }

    partial void OnLaunchOnStartupChanged(bool value)
    {
        StartupService.SetStartup(value);
    }

    [RelayCommand]
    private void LaunchRecentSession(RecentSession? session)
    {
        if (session == null) return;
        try
        {
            // Try Windows Terminal first
            var psi = new ProcessStartInfo("wt.exe",
                $"new-tab --startingDirectory \"{session.FolderPath}\" -- claude")
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            try
            {
                // Fallback to cmd
                var psi = new ProcessStartInfo("cmd.exe",
                    $"/k cd /d \"{session.FolderPath}\" && claude")
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { }
        }
    }

    [RelayCommand]
    private void NewSession()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select project folder"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var psi = new ProcessStartInfo("wt.exe",
                    $"new-tab --startingDirectory \"{dialog.FolderName}\" -- claude")
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                try
                {
                    var psi = new ProcessStartInfo("cmd.exe",
                        $"/k cd /d \"{dialog.FolderName}\" && claude")
                    {
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch { }
            }
        }
    }
}
