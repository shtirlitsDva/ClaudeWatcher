namespace ClaudeWatcher.Models;

public class AppSettings
{
    public bool LaunchOnStartup { get; set; }
    public int HttpPort { get; set; } = 22322;
    public double WindowOpacity { get; set; } = 0.95;
    public int MaxWindowHeight { get; set; } = 600;
}
