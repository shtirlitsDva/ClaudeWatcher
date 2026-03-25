namespace ClaudeWatcher.Models;

public record RecentSession(
    string ProjectName,
    string FolderPath,
    string? Branch,
    string? WorktreeName,
    DateTime LastActiveUtc);
