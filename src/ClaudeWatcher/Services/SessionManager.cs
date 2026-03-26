using System.Collections.Concurrent;
using ClaudeWatcher.Models;

namespace ClaudeWatcher.Services;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    public event Action<SessionInfo>? SessionAdded;
    public event Action<SessionInfo>? SessionUpdated;
    public event Action<SessionInfo>? SessionRemoved;

    public IReadOnlyDictionary<string, SessionInfo> ActiveSessions => _sessions;

    public void RegisterSession(SessionStartPayload payload)
    {
        if (string.IsNullOrEmpty(payload.session_id)) return;

        var session = new SessionInfo
        {
            SessionId = payload.session_id,
            WorkingDirectory = payload.cwd ?? "",
            Model = payload.model,
            Status = SessionStatus.Idle,
            StartedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow,
            TabTitle = $"CW:{payload.session_id[..Math.Min(8, payload.session_id.Length)]}",
            ShellPid = payload.shell_pid ?? 0
        };

        if (_sessions.TryAdd(payload.session_id, session))
        {
            Log.Info($"Session registered: {payload.session_id} ({payload.cwd})");
            SessionAdded?.Invoke(session);
        }
    }

    public void UpdateSession(SessionUpdatePayload payload)
    {
        Log.Info($"Update received: {payload.hook_event_name} for {payload.session_id?[..Math.Min(8, payload.session_id?.Length ?? 0)]}");
        if (string.IsNullOrEmpty(payload.session_id)) return;
        if (!_sessions.TryGetValue(payload.session_id, out var session))
        {
            // Auto-register if we get an update for an unknown session
            RegisterSession(new SessionStartPayload(
                payload.session_id, payload.cwd, null,
                payload.hook_event_name, null, payload.timestamp, null));
            if (!_sessions.TryGetValue(payload.session_id, out session)) return;
        }

        session.LastUpdatedUtc = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(payload.last_assistant_message))
        {
            session.LastMessage = payload.last_assistant_message.Length > 200
                ? payload.last_assistant_message[..200]
                : payload.last_assistant_message;
        }

        if (payload.used_percentage.HasValue)
            session.ContextUsedPercentage = payload.used_percentage.Value;

        if (payload.context_window_size.HasValue && payload.context_window_size.Value > 0)
            session.ContextWindowSize = payload.context_window_size.Value;

        session.Status = payload.hook_event_name switch
        {
            "UserPromptSubmit" => SessionStatus.Working,
            "PreToolUse" => SessionStatus.Tool,
            "PostToolUse" => SessionStatus.Working,
            "PermissionRequest" => SessionStatus.Permission,
            "Stop" => payload.stop_hook_active == true ? SessionStatus.Working : SessionStatus.Idle,
            "StopFailure" => SessionStatus.Error,
            _ => session.Status
        };

        SessionUpdated?.Invoke(session);
    }

    public void NotifySession(SessionNotificationPayload payload)
    {
        if (string.IsNullOrEmpty(payload.session_id)) return;
        if (!_sessions.TryGetValue(payload.session_id, out var session)) return;

        session.LastUpdatedUtc = DateTime.UtcNow;

        session.Status = payload.notification_type switch
        {
            "permission_prompt" or "elicitation_dialog" => SessionStatus.Waiting,
            "idle_prompt" => SessionStatus.Idle,
            _ => session.Status
        };

        if (!string.IsNullOrEmpty(payload.message))
        {
            session.LastMessage = payload.message.Length > 200
                ? payload.message[..200]
                : payload.message;
        }

        SessionUpdated?.Invoke(session);
    }

    public void HandleSubagent(SubagentPayload payload)
    {
        if (string.IsNullOrEmpty(payload.session_id)) return;
        if (!_sessions.TryGetValue(payload.session_id, out var session)) return;

        session.LastUpdatedUtc = DateTime.UtcNow;

        if (payload.hook_event_name == "SubagentStart" && !string.IsNullOrEmpty(payload.agent_id))
        {
            session.ActiveSubagents.Add(payload.agent_id);
        }
        else if (payload.hook_event_name == "SubagentStop" && !string.IsNullOrEmpty(payload.agent_id))
        {
            session.ActiveSubagents.Remove(payload.agent_id);
        }
        session.ActiveSubagentCount = session.ActiveSubagents.Count;

        SessionUpdated?.Invoke(session);
    }

    public void RemoveSession(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        if (_sessions.TryRemove(sessionId, out var session))
        {
            Log.Info($"Session removed: {sessionId}");
            SessionRemoved?.Invoke(session);
        }
    }
}
