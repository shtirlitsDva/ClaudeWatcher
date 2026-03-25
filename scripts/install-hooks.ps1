#!/usr/bin/env pwsh
# ClaudeWatcher hook installer (PowerShell)
# Adds session monitoring hooks to ~/.claude/settings.json

$ErrorActionPreference = "Stop"

$settingsFile = Join-Path $env:USERPROFILE ".claude\settings.json"
$watcherUrl = "http://127.0.0.1:22322"

# Create settings file if missing
$settingsDir = Split-Path $settingsFile
if (-not (Test-Path $settingsDir)) { New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null }
if (-not (Test-Path $settingsFile)) { '{}' | Set-Content $settingsFile -Encoding UTF8 }

# Read current settings
$settings = Get-Content $settingsFile -Raw | ConvertFrom-Json

# Ensure hooks object exists
if (-not $settings.PSObject.Properties['hooks']) {
    $settings | Add-Member -NotePropertyName 'hooks' -NotePropertyValue ([PSCustomObject]@{})
}

# Hook commands - all errors suppressed, always exit 0
$hooks = @{
    SessionStart = @{
        command = "bash -c 'input=`$(cat); sid=`$(echo ""`$input"" | grep -o """"""session_id"""":""""""[^""""""]*"""""" 2>/dev/null | head -1 | cut -d"""""" -f4); short=`${sid:0:8}; echo -ne ""\\033]0;CW:`${short}\\007"" > /dev/tty 2>/dev/null; curl -sf $watcherUrl/api/health > /dev/null 2>&1 || (start """" ""%LOCALAPPDATA%/ClaudeWatcher/ClaudeWatcher.exe"" 2>/dev/null; sleep 2); curl -sf -X POST $watcherUrl/api/session/start -H ""Content-Type: application/json"" -d ""`$input"" > /dev/null 2>&1; echo ""{\\""additionalContext\\"": \\""ClaudeWatcher is monitoring this session. Focus on your work as normal.\\""}\""; exit 0'"
        timeout = 10
    }
    Stop = @{
        command = "bash -c 'input=`$(cat); curl -sf -X POST $watcherUrl/api/session/update -H ""Content-Type: application/json"" -d ""`$input"" > /dev/null 2>&1; exit 0'"
        timeout = 5
    }
    Notification = @{
        command = "bash -c 'input=`$(cat); curl -sf -X POST $watcherUrl/api/session/notification -H ""Content-Type: application/json"" -d ""`$input"" > /dev/null 2>&1; exit 0'"
        timeout = 5
    }
    SessionEnd = @{
        command = "bash -c 'input=`$(cat); curl -sf -X POST $watcherUrl/api/session/end -H ""Content-Type: application/json"" -d ""`$input"" > /dev/null 2>&1; exit 0'"
        timeout = 5
    }
}

foreach ($eventName in $hooks.Keys) {
    $hookDef = $hooks[$eventName]

    $newEntry = [PSCustomObject]@{
        matcher = ""
        hooks = @(
            [PSCustomObject]@{
                type = "command"
                command = $hookDef.command
                timeout = $hookDef.timeout
            }
        )
    }

    # Get existing entries for this event, filter out ClaudeWatcher ones
    $existing = @()
    if ($settings.hooks.PSObject.Properties[$eventName]) {
        $existing = @($settings.hooks.$eventName | Where-Object {
            $cmd = $_.hooks[0].command
            $cmd -notlike "*$watcherUrl*"
        })
    }

    # Append new entry
    $existing += $newEntry

    # Set on the hooks object
    if ($settings.hooks.PSObject.Properties[$eventName]) {
        $settings.hooks.$eventName = $existing
    } else {
        $settings.hooks | Add-Member -NotePropertyName $eventName -NotePropertyValue $existing
    }
}

# Write back with pretty formatting
$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsFile -Encoding UTF8

Write-Host "ClaudeWatcher hooks installed successfully." -ForegroundColor Green
Write-Host "  SessionStart: registers session + sets tab title + auto-launches watcher"
Write-Host "  Stop: updates session status and message"
Write-Host "  Notification: flags session as needing attention"
Write-Host "  SessionEnd: removes session card"
Write-Host ""
Write-Host "Hooks are in: $settingsFile"
