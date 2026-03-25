#!/bin/bash
# ClaudeWatcher hook uninstaller
# Removes ClaudeWatcher hooks from ~/.claude/settings.json

set -e

SETTINGS_FILE="$HOME/.claude/settings.json"
WATCHER_URL="http://127.0.0.1:22322"

if ! command -v jq &>/dev/null; then
    echo "Error: jq is required."
    exit 1
fi

if [ ! -f "$SETTINGS_FILE" ]; then
    echo "No settings file found at $SETTINGS_FILE"
    exit 0
fi

CURRENT=$(cat "$SETTINGS_FILE")

# For each hook event, remove entries containing the watcher URL
for event in SessionStart Stop Notification SessionEnd; do
    CURRENT=$(echo "$CURRENT" | jq --arg event "$event" --arg marker "$WATCHER_URL" '
        if .hooks[$event] then
            .hooks[$event] |= map(select(.hooks[0].command | tostring | contains($marker) | not))
            | if .hooks[$event] | length == 0 then del(.hooks[$event]) else . end
        else . end
    ')
done

# Remove hooks object if empty
CURRENT=$(echo "$CURRENT" | jq 'if .hooks | length == 0 then del(.hooks) else . end')

echo "$CURRENT" | jq '.' > "$SETTINGS_FILE"

echo "ClaudeWatcher hooks removed from $SETTINGS_FILE"
