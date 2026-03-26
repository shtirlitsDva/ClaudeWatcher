using System.Windows;
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
    private Brush _contextBackgroundBrush = Brushes.Transparent;

    [ObservableProperty]
    private string? _tabTitle;

    [ObservableProperty]
    private int _shellPid;

    [ObservableProperty]
    private int _activeSubagentCount;

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

        ActiveSubagentCount = session.ActiveSubagentCount;
        if (session.ShellPid > 0) ShellPid = session.ShellPid;
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

        var color = free switch
        {
            <= 10 => Color.FromArgb(0x80, 0xFF, 0x44, 0x44),
            <= 30 => Color.FromArgb(0x70, 0xFF, 0xB8, 0x00),
            _ => Color.FromArgb(0x60, 0x4C, 0xAF, 0x50)
        };
        var transparent = Color.FromArgb(0, color.R, color.G, color.B);
        double fraction = Math.Clamp(free / 100.0, 0, 1);

        ContextBackgroundBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops = new GradientStopCollection
            {
                new(color, 0),
                new(color, Math.Max(0, fraction - 0.15)),
                new(transparent, fraction)
            }
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
