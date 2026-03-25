using System.Windows.Media;
using System.Windows.Threading;
using ClaudeWatcher.Helpers;
using ClaudeWatcher.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeWatcher.ViewModels;

public partial class SessionCardViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private DateTime _startedUtc = DateTime.UtcNow;

    [ObservableProperty]
    private string _sessionId = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private SessionStatus _status = SessionStatus.Working;

    [ObservableProperty]
    private string _modelName = string.Empty;

    [ObservableProperty]
    private string _elapsedTime = string.Empty;

    [ObservableProperty]
    private double _contextFreePercentage = 100;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private SolidColorBrush _contextBarBrush = new(Color.FromRgb(0x4C, 0xAF, 0x50));

    [ObservableProperty]
    private double _contextBarWidth;

    [ObservableProperty]
    private string? _tabTitle;

    public string DisplayMessage => string.IsNullOrEmpty(Message)
        ? WorkingDirectory
        : Message;

    public Action<SessionCardViewModel>? CardClicked { get; set; }

    public SessionCardViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateElapsedTime();
        _timer.Start();
    }

    public void UpdateFrom(SessionInfo session)
    {
        SessionId = session.SessionId;
        WorkingDirectory = session.WorkingDirectory;
        Status = session.Status;
        TabTitle = session.TabTitle;

        if (!string.IsNullOrEmpty(session.Model))
            ModelName = FormatModelName(session.Model);

        if (!string.IsNullOrEmpty(session.Title))
            Title = session.Title;
        else if (string.IsNullOrEmpty(Title))
            Title = GitInfoHelper.GetDefaultTitle(session.WorkingDirectory);

        if (!string.IsNullOrEmpty(session.LastMessage))
        {
            Message = session.LastMessage;
            OnPropertyChanged(nameof(DisplayMessage));
        }

        _startedUtc = session.StartedUtc;
        UpdateContextBar(session.ContextUsedPercentage, session.ContextWindowSize);
        UpdateElapsedTime();
    }

    public void UpdateContextBar(double usedPct, int windowSize)
    {
        if (windowSize <= 0) windowSize = 200000;
        int used = (int)(windowSize * usedPct / 100);
        int free = (windowSize - used - 33000) * 100 / windowSize;
        free = Math.Max(0, Math.Min(100, free));
        ContextFreePercentage = free;

        // Bar shows how much is USED (inverse of free)
        ContextBarWidth = Math.Max(0, Math.Min(100, 100 - free));

        ContextBarBrush = free switch
        {
            <= 10 => new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0x44, 0x44)), // red
            <= 30 => new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xB8, 0x00)), // yellow
            _ => new SolidColorBrush(Color.FromArgb(0x99, 0x4C, 0xAF, 0x50))      // green
        };
    }

    private void UpdateElapsedTime()
    {
        var span = DateTime.UtcNow - _startedUtc;
        ElapsedTime = span switch
        {
            { TotalHours: >= 2 } => $"{(int)span.TotalHours}h+",
            { TotalHours: >= 1 } => $"{(int)span.TotalHours}h {span.Minutes}m",
            { TotalMinutes: >= 1 } => $"{(int)span.TotalMinutes}m",
            _ => $"{(int)span.TotalSeconds}s"
        };
    }

    private static string FormatModelName(string model)
    {
        // "claude-opus-4-6" → "Opus 4.6"
        if (model.Contains("opus", StringComparison.OrdinalIgnoreCase))
            return model.Contains("4-6") || model.Contains("4.6") ? "Opus 4.6" : "Opus";
        if (model.Contains("sonnet", StringComparison.OrdinalIgnoreCase))
            return model.Contains("4-6") || model.Contains("4.6") ? "Sonnet 4.6" : "Sonnet";
        if (model.Contains("haiku", StringComparison.OrdinalIgnoreCase))
            return "Haiku";
        return model.Length > 12 ? model[..12] : model;
    }

    public void OnCardClicked()
    {
        CardClicked?.Invoke(this);
    }

    public void StopTimer()
    {
        _timer.Stop();
    }
}
