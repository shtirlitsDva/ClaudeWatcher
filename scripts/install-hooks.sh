#!/bin/bash
# ClaudeWatcher hook installer
# Adds session monitoring hooks to ~/.claude/settings.json

set -e

SETTINGS_FILE="$HOME/.claude/settings.json"
WATCHER_URL="http://127.0.0.1:22322"

# Use deployed hooks directory under %LOCALAPPDATA%\ClaudeWatcher\hooks
HOOKS_DIR="${LOCALAPPDATA}/ClaudeWatcher/hooks"
if [ ! -d "$HOOKS_DIR" ]; then
    echo "ERROR: Hook scripts not found at $HOOKS_DIR"
    echo "Run Deploy.bat first to build and copy hook scripts."
    exit 1
fi

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

# Hook commands — reference deployed script files
# Timeouts are in SECONDS (Claude hooks API)
SESSION_START_CMD="bash '$HOOKS_DIR/session-start.sh'"
STOP_CMD="bash '$HOOKS_DIR/session-update.sh'"
NOTIFICATION_CMD="bash '$HOOKS_DIR/session-notification.sh'"
SESSION_END_CMD="bash '$HOOKS_DIR/session-end.sh'"

# Build hook entries as JSON
build_hook_entry() {
    local cmd="$1"
    local timeout="${2:-5}"
    jq -n --arg cmd "$cmd" --argjson timeout "$timeout" '{
        hooks: [{
            type: "command",
            command: $cmd,
            timeout: $timeout
        }]
    }'
}

SESSION_START_HOOK=$(build_hook_entry "$SESSION_START_CMD" 30)
STOP_HOOK=$(build_hook_entry "$STOP_CMD" 5)
NOTIFICATION_HOOK=$(build_hook_entry "$NOTIFICATION_CMD" 5)
SESSION_END_HOOK=$(build_hook_entry "$SESSION_END_CMD" 5)

# Remove existing ClaudeWatcher hooks (if any) and add new ones
add_hook() {
    local event="$1"
    local new_hook="$2"
    local result

    # Get existing hooks for this event, filter out ClaudeWatcher ones
    result=$(echo "$CURRENT" | jq --arg event "$event" --arg marker "$WATCHER_URL" --arg marker2 "ClaudeWatcher" \
        '(.hooks[$event] // []) | map(select(
            (.hooks[0].command | tostring) as $cmd |
            (($cmd | contains($marker)) or ($cmd | contains($marker2)) or
             ($cmd | contains("session-start.sh")) or ($cmd | contains("session-update.sh")) or
             ($cmd | contains("session-notification.sh")) or ($cmd | contains("session-end.sh"))) | not
        ))')

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
echo "Hook scripts in: $HOOKS_DIR"
echo "Hooks config in: $SETTINGS_FILE"
