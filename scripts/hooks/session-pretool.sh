#!/bin/bash
# PreToolUse — Claude is about to execute a tool
WATCHER_URL="http://127.0.0.1:22322"
input=$(cat)
curl -sf --connect-timeout 2 --max-time 3 -X POST "$WATCHER_URL/api/session/update" \
    -H "Content-Type: application/json" \
    -d "$input" > /dev/null 2>&1
exit 0
