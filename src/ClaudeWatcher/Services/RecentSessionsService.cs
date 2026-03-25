using System.IO;
using System.Text.Json;
using ClaudeWatcher.Models;

namespace ClaudeWatcher.Services;

public class RecentSessionsService
{
    private readonly string _filePath;
    private List<RecentSession> _sessions = new();

    public IReadOnlyList<RecentSession> Sessions => _sessions;

    public RecentSessionsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = Path.Combine(appData, "ClaudeWatcher");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "recent-sessions.json");
        Load();
    }

    public void AddOrUpdate(SessionInfo session)
    {
        string folder = Path.GetFileName(session.WorkingDirectory.TrimEnd('/', '\\')) ?? "Unknown";
        string? branch = null;
        try
        {
            branch = Helpers.GitInfoHelper.GetDefaultTitle(session.WorkingDirectory);
            // Extract branch part after " - "
            int dashIdx = branch.IndexOf(" - ");
            if (dashIdx >= 0) branch = branch[(dashIdx + 3)..];
            else branch = null;
        }
        catch { }

        var entry = new RecentSession(
            folder,
            session.WorkingDirectory,
            branch,
            null,
            DateTime.UtcNow);

        // Remove existing entry for same folder
        _sessions.RemoveAll(s =>
            string.Equals(s.FolderPath, session.WorkingDirectory, StringComparison.OrdinalIgnoreCase));

        _sessions.Insert(0, entry);

        // Keep max 20
        if (_sessions.Count > 20)
            _sessions.RemoveRange(20, _sessions.Count - 20);

        Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                _sessions = JsonSerializer.Deserialize<List<RecentSession>>(json) ?? new();
            }
        }
        catch
        {
            _sessions = new();
        }
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_sessions, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }
}
