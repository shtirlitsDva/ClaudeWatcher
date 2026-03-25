namespace ClaudeWatcher.Models;

public class SessionInfo
{
    public string SessionId { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string? Model { get; set; }
    public string? Title { get; set; }
    public string? LastMessage { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Idle;
    public double ContextUsedPercentage { get; set; }
    public int ContextWindowSize { get; set; } = 200000;
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public string? TabTitle { get; set; }
    public int ActiveSubagentCount { get; set; }
    public HashSet<string> ActiveSubagents { get; set; } = new();
}

public enum SessionStatus { Idle, Working, Tool, Permission, Waiting, Error }
