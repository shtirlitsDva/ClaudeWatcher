#!/usr/bin/env pwsh
# ClaudeWatcher hook installer (PowerShell)
# Adds session monitoring hooks to ~/.claude/settings.json

$ErrorActionPreference = "Stop"

$settingsFile = Join-Path $env:USERPROFILE ".claude\settings.json"
$watcherUrl = "http://127.0.0.1:22322"

# Use deployed hooks directory under %LOCALAPPDATA%\ClaudeWatcher\hooks
$hooksDir = Join-Path $env:LOCALAPPDATA "ClaudeWatcher\hooks"
if (-not (Test-Path $hooksDir)) {
    Write-Host "ERROR: Hook scripts not found at $hooksDir" -ForegroundColor Red
    Write-Host "Run Deploy.bat first to build and copy hook scripts." -ForegroundColor Yellow
    exit 1
}

# Convert to forward-slash unix paths for bash
$hooksDir = $hooksDir -replace '\\','/'

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

# Hook definitions — timeouts are in SECONDS (Claude hooks API)
# Some hooks need a matcher property (PreToolUse, PermissionRequest, etc.)
$hooks = [ordered]@{
    SessionStart = @{ command = "bash '$hooksDir/session-start.sh'"; timeout = 30 }
    UserPromptSubmit = @{ command = "bash '$hooksDir/session-prompt.sh'"; timeout = 5 }
    PreToolUse = @{ command = "bash '$hooksDir/session-pretool.sh'"; timeout = 5; matcher = ".*" }
    PermissionRequest = @{ command = "bash '$hooksDir/session-permission.sh'"; timeout = 5; matcher = ".*" }
    PostToolUse = @{ command = "bash '$hooksDir/session-posttool.sh'"; timeout = 5; matcher = ".*" }
    SubagentStart = @{ command = "bash '$hooksDir/session-subagent-start.sh'"; timeout = 5; matcher = ".*" }
    SubagentStop = @{ command = "bash '$hooksDir/session-subagent-stop.sh'"; timeout = 5; matcher = ".*" }
    Stop = @{ command = "bash '$hooksDir/session-update.sh'"; timeout = 5 }
    StopFailure = @{ command = "bash '$hooksDir/session-stopfailure.sh'"; timeout = 5 }
    Notification = @{ command = "bash '$hooksDir/session-notification.sh'"; timeout = 5 }
    SessionEnd = @{ command = "bash '$hooksDir/session-end.sh'"; timeout = 5 }
}

foreach ($eventName in $hooks.Keys) {
    $hookDef = $hooks[$eventName]

    $newEntry = [PSCustomObject]@{
        hooks = @(
            [PSCustomObject]@{
                type = "command"
                command = $hookDef.command
                timeout = $hookDef.timeout
            }
        )
    }
    # Add matcher if specified
    if ($hookDef.ContainsKey('matcher')) {
        $newEntry | Add-Member -NotePropertyName 'matcher' -NotePropertyValue $hookDef.matcher
    }

    # Get existing entries for this event, filter out ClaudeWatcher ones
    $existing = @()
    if ($settings.hooks.PSObject.Properties[$eventName]) {
        $existing = @($settings.hooks.$eventName | Where-Object {
            $cmd = $_.hooks[0].command
            ($cmd -notlike "*$watcherUrl*") -and ($cmd -notlike "*ClaudeWatcher*") -and ($cmd -notlike "*session-*.sh*")
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
Write-Host "Hook scripts in: $hooksDir"
Write-Host "Hooks config in: $settingsFile"
