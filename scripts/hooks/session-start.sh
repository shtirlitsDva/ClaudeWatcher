#!/bin/bash
# ClaudeWatcher SessionStart hook
# Launched by Claude Code on every new session start.
# Ensures ClaudeWatcher is running, then registers the session.

WATCHER_URL="http://127.0.0.1:22322"
CW_EXE="${LOCALAPPDATA}/ClaudeWatcher/ClaudeWatcher.exe"

input=$(cat)

# Extract session_id for terminal title
sid=$(echo "$input" | grep -o '"session_id":"[^"]*"' 2>/dev/null | head -1 | cut -d'"' -f4)
short=${sid:0:8}
echo -ne "\033]0;CW:${short}\007" > /dev/tty 2>/dev/null

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
    -d "$input" > /dev/null 2>&1

echo '{"additionalContext": "ClaudeWatcher is monitoring this session."}'
exit 0
