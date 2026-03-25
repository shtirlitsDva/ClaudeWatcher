#!/bin/bash
# ClaudeWatcher hook installer
# Adds session monitoring hooks to ~/.claude/settings.json

set -e

SETTINGS_FILE="$HOME/.claude/settings.json"
WATCHER_URL="http://127.0.0.1:22322"
MARKER="ClaudeWatcher"

if ! command -v jq &>/dev/null; then
    echo "Error: jq is required. Install from https://jqlang.github.io/jq/download/"
    exit 1
fi

# Create settings file if it doesn't exist
if [ ! -f "$SETTINGS_FILE" ]; then
    mkdir -p "$(dirname "$SETTINGS_FILE")"
    echo '{}' > "$SETTINGS_FILE"
fi

# Read current settings
CURRENT=$(cat "$SETTINGS_FILE")

# Define hook commands — all errors suppressed, always exit 0
SESSION_START_CMD="bash -c 'input=\$(cat); sid=\$(echo \"\$input\" | jq -r \".session_id // empty\" 2>/dev/null); short=\${sid:0:8}; echo -ne \"\\\\033]0;CW:\${short}\\\\007\" > /dev/tty 2>/dev/null; curl -sf ${WATCHER_URL}/api/health > /dev/null 2>&1 || (start \"\" \"\${LOCALAPPDATA}/ClaudeWatcher/ClaudeWatcher.exe\" 2>/dev/null; sleep 2); curl -sf -X POST ${WATCHER_URL}/api/session/start -H \"Content-Type: application/json\" -d \"\$input\" > /dev/null 2>&1; echo \"{\\\\\"additionalContext\\\\\": \\\\\"${MARKER} is monitoring this session. Focus on your work as normal.\\\\\"}\"; exit 0'"

STOP_CMD="bash -c 'input=\$(cat); curl -sf -X POST ${WATCHER_URL}/api/session/update -H \"Content-Type: application/json\" -d \"\$input\" > /dev/null 2>&1; exit 0'"

NOTIFICATION_CMD="bash -c 'input=\$(cat); curl -sf -X POST ${WATCHER_URL}/api/session/notification -H \"Content-Type: application/json\" -d \"\$input\" > /dev/null 2>&1; exit 0'"

SESSION_END_CMD="bash -c 'input=\$(cat); curl -sf -X POST ${WATCHER_URL}/api/session/end -H \"Content-Type: application/json\" -d \"\$input\" > /dev/null 2>&1; exit 0'"

# Build hook entries as JSON
build_hook_entry() {
    local cmd="$1"
    local timeout="${2:-5}"
    jq -n --arg cmd "$cmd" --argjson timeout "$timeout" '{
        matcher: "",
        hooks: [{
            type: "command",
            command: $cmd,
            timeout: $timeout
        }]
    }'
}

SESSION_START_HOOK=$(build_hook_entry "$SESSION_START_CMD" 10)
STOP_HOOK=$(build_hook_entry "$STOP_CMD" 5)
NOTIFICATION_HOOK=$(build_hook_entry "$NOTIFICATION_CMD" 5)
SESSION_END_HOOK=$(build_hook_entry "$SESSION_END_CMD" 5)

# Remove existing ClaudeWatcher hooks (if any) and add new ones
# For each event, filter out entries containing the watcher URL, then append new one
add_hook() {
    local event="$1"
    local new_hook="$2"
    local result

    # Get existing hooks for this event, filter out ClaudeWatcher ones
    result=$(echo "$CURRENT" | jq --arg event "$event" --arg marker "$WATCHER_URL" \
        '(.hooks[$event] // []) | map(select(.hooks[0].command | tostring | contains($marker) | not))')

    # Append new hook
    result=$(echo "$result" | jq --argjson hook "$new_hook" '. + [$hook]')

    # Update current settings
    CURRENT=$(echo "$CURRENT" | jq --arg event "$event" --argjson hooks "$result" \
        '.hooks[$event] = $hooks')
}

# Ensure hooks object exists
CURRENT=$(echo "$CURRENT" | jq '.hooks //= {}')

add_hook "SessionStart" "$SESSION_START_HOOK"
add_hook "Stop" "$STOP_HOOK"
add_hook "Notification" "$NOTIFICATION_HOOK"
add_hook "SessionEnd" "$SESSION_END_HOOK"

# Write back
echo "$CURRENT" | jq '.' > "$SETTINGS_FILE"

echo "ClaudeWatcher hooks installed successfully."
echo "  SessionStart: registers session + sets tab title + auto-launches watcher"
echo "  Stop: updates session status and message"
echo "  Notification: flags session as needing attention"
echo "  SessionEnd: removes session card"
echo ""
echo "Hooks are in: $SETTINGS_FILE"
