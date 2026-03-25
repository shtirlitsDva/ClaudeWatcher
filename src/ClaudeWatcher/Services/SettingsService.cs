using System.IO;
using System.Text.Json;
using ClaudeWatcher.Models;

namespace ClaudeWatcher.Services;

public class SettingsService
{
    private readonly string _filePath;
    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = Path.Combine(appData, "ClaudeWatcher");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        Load();
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            }
        }
        catch
        {
            Settings = new();
        }
    }
}
