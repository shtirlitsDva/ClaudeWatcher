namespace ClaudeWatcher.Models;

public record SessionStartPayload(
    string? session_id, string? cwd, string? model,
    string? hook_event_name, string? source, string? timestamp);

public record SessionUpdatePayload(
    string? session_id, string? cwd, string? hook_event_name,
    string? last_assistant_message, bool? stop_hook_active,
    double? used_percentage, int? context_window_size, string? timestamp);

public record SessionNotificationPayload(
    string? session_id, string? notification_type,
    string? message, string? hook_event_name, string? timestamp);

public record SessionEndPayload(
    string? session_id, string? hook_event_name,
    string? source, string? timestamp);
