#!/bin/bash
# ClaudeWatcher SessionStart hook
# Launched by Claude Code on every new session start.
# Ensures ClaudeWatcher is running, then registers the session.

WATCHER_URL="http://127.0.0.1:22322"
CW_EXE="${LOCALAPPDATA}/ClaudeWatcher/ClaudeWatcher.exe"

input=$(cat)

# Inject shell PID so ClaudeWatcher can find the terminal window
enriched=$(echo "$input" | sed "s/}$/,\"shell_pid\":$PPID}/")

# If CW is not running, launch it and wait for it to be ready
if ! curl -sf --connect-timeout 2 --max-time 3 "$WATCHER_URL/api/health" > /dev/null 2>&1; then
    cmd //c start "" "$CW_EXE" </dev/null >/dev/null 2>&1
    for i in 1 2 3 4 5 6 7 8; do
        sleep 1
        curl -sf --connect-timeout 2 --max-time 3 "$WATCHER_URL/api/health" > /dev/null 2>&1 && break
    done
fi

# Register session
curl -sf --connect-timeout 2 --max-time 3 -X POST "$WATCHER_URL/api/session/start" \
    -H "Content-Type: application/json" \
    -d "$enriched" > /dev/null 2>&1

echo '{"additionalContext": "ClaudeWatcher is monitoring this session."}'
exit 0
