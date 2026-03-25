using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeWatcher.ViewModels;

public enum SessionStatus
{
    Working,
    Waiting,
    Error,
    Idle
}

public partial class SessionCardViewModel : ObservableObject
{
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
}
