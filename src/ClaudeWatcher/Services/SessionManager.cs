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
            Status = SessionStatus.Working,
            StartedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow,
            TabTitle = $"CW:{payload.session_id[..Math.Min(8, payload.session_id.Length)]}"
        };

        if (_sessions.TryAdd(payload.session_id, session))
        {
            SessionAdded?.Invoke(session);
        }
    }

    public void UpdateSession(SessionUpdatePayload payload)
    {
        if (string.IsNullOrEmpty(payload.session_id)) return;
        if (!_sessions.TryGetValue(payload.session_id, out var session))
        {
            // Auto-register if we get an update for an unknown session
            RegisterSession(new SessionStartPayload(
                payload.session_id, payload.cwd, null,
                payload.hook_event_name, null, payload.timestamp));
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

        // Status mapping from Stop hook
        if (payload.hook_event_name == "Stop")
        {
            session.Status = payload.stop_hook_active == true
                ? SessionStatus.Working
                : SessionStatus.Idle;
        }

        SessionUpdated?.Invoke(session);
    }

    public void NotifySession(SessionNotificationPayload payload)
    {
        if (string.IsNullOrEmpty(payload.session_id)) return;
        if (!_sessions.TryGetValue(payload.session_id, out var session)) return;

        session.LastUpdatedUtc = DateTime.UtcNow;

        session.Status = payload.notification_type switch
        {
            "permission_prompt" or "idle_prompt" or "elicitation_dialog" => SessionStatus.Waiting,
            _ => SessionStatus.Error
        };

        if (!string.IsNullOrEmpty(payload.message))
        {
            session.LastMessage = payload.message.Length > 200
                ? payload.message[..200]
                : payload.message;
        }

        SessionUpdated?.Invoke(session);
    }

    public void RemoveSession(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        if (_sessions.TryRemove(sessionId, out var session))
        {
            SessionRemoved?.Invoke(session);
        }
    }

    public void CleanupStale()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _sessions)
        {
            var age = now - kvp.Value.LastUpdatedUtc;
            if (age > TimeSpan.FromMinutes(30))
            {
                RemoveSession(kvp.Key);
            }
            else if (age > TimeSpan.FromMinutes(5) && kvp.Value.Status == SessionStatus.Working)
            {
                kvp.Value.Status = SessionStatus.Waiting;
                kvp.Value.LastMessage = "Session may be stalled";
                SessionUpdated?.Invoke(kvp.Value);
            }
        }
    }
}
