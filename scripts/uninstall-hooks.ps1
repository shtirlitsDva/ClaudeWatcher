#!/usr/bin/env pwsh
# ClaudeWatcher hook uninstaller (PowerShell)

$settingsFile = Join-Path $env:USERPROFILE ".claude\settings.json"

if (-not (Test-Path $settingsFile)) {
    Write-Host "No settings file found at $settingsFile"
    exit 0
}

$settings = Get-Content $settingsFile -Raw | ConvertFrom-Json

if (-not $settings.PSObject.Properties['hooks']) {
    Write-Host "No hooks configured."
    exit 0
}

# Process ALL hook event names — filter out any entry whose command contains "ClaudeWatcher"
foreach ($eventName in @($settings.hooks.PSObject.Properties.Name)) {
    $entries = $settings.hooks.$eventName
    if ($null -eq $entries) { continue }

    $filtered = @($entries | Where-Object {
        $cmd = $_.hooks[0].command
        $cmd -notlike "*ClaudeWatcher*"
    })

    if ($filtered.Count -eq 0) {
        $settings.hooks.PSObject.Properties.Remove($eventName)
    } else {
        $settings.hooks.$eventName = $filtered
    }
}

# Remove hooks object if empty
$remaining = @($settings.hooks.PSObject.Properties)
if ($remaining.Count -eq 0) {
    $settings.PSObject.Properties.Remove('hooks')
}

$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsFile -Encoding UTF8

Write-Host "ClaudeWatcher hooks removed from $settingsFile" -ForegroundColor Green
