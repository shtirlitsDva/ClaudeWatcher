#!/usr/bin/env pwsh
# ClaudeWatcher hook uninstaller (PowerShell)

$settingsFile = Join-Path $env:USERPROFILE ".claude\settings.json"
$watcherUrl = "http://127.0.0.1:22322"

if (-not (Test-Path $settingsFile)) {
    Write-Host "No settings file found at $settingsFile"
    exit 0
}

$settings = Get-Content $settingsFile -Raw | ConvertFrom-Json

if (-not $settings.PSObject.Properties['hooks']) {
    Write-Host "No hooks configured."
    exit 0
}

foreach ($eventName in @('SessionStart', 'Stop', 'Notification', 'SessionEnd')) {
    if ($settings.hooks.PSObject.Properties[$eventName]) {
        $filtered = @($settings.hooks.$eventName | Where-Object {
            $_.hooks[0].command -notlike "*$watcherUrl*"
        })
        if ($filtered.Count -eq 0) {
            $settings.hooks.PSObject.Properties.Remove($eventName)
        } else {
            $settings.hooks.$eventName = $filtered
        }
    }
}

# Remove hooks object if empty
$remaining = @($settings.hooks.PSObject.Properties)
if ($remaining.Count -eq 0) {
    $settings.PSObject.Properties.Remove('hooks')
}

$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsFile -Encoding UTF8

Write-Host "ClaudeWatcher hooks removed from $settingsFile" -ForegroundColor Green
