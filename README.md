# ClaudeWatcher

A dark-mode, always-on-top desktop app that monitors all your active Claude Code sessions at a glance.

![Windows](https://img.shields.io/badge/platform-Windows-blue)
![.NET 10](https://img.shields.io/badge/.NET-10-purple)
![License](https://img.shields.io/badge/license-MIT-green)

## What It Does

ClaudeWatcher sits in the corner of your screen and shows you what every Claude Code session is doing — without switching terminals.

- **Status indicators** — animated spinner while Claude is working, flashing yellow when waiting for your input, flashing red on errors, solid green when idle
- **Context usage bar** — see how much context each session has consumed (green → yellow → red)
- **Session titles** — project name, git branch, and worktree detected automatically
- **Message snippets** — last thing Claude said, right on the card
- **One-click focus** — click a card to bring that exact terminal tab to the foreground, even inside Windows Terminal
- **Right-click menu** — recent sessions, launch new sessions, minimize to tray, startup settings

All data flows through Claude Code's native hook system. No file polling, no custom protocols.

## Screenshots

*Coming soon*

## Prerequisites

- **Windows 10/11**
- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Claude Code CLI** — installed and working
- **jq** — required for the hook install script ([download](https://jqlang.github.io/jq/download/))
- **curl** — included with Git Bash / Windows 10+

## Quick Start

### 1. Clone and build

```bash
git clone https://github.com/shtirlitsDva/ClaudeWatcher.git
cd ClaudeWatcher
dotnet build src/ClaudeWatcher/ClaudeWatcher.csproj
```

### 2. Install the hooks

```bash
bash scripts/install-hooks.sh
```

This adds four hooks to your `~/.claude/settings.json`:
- **SessionStart** — registers the session, sets the terminal tab title, auto-launches ClaudeWatcher if it's not running
- **Stop** — sends Claude's latest message and context usage to the watcher
- **Notification** — flags the session when Claude needs your attention (permission prompts, errors)
- **SessionEnd** — removes the session card

Your existing hooks are preserved — the script only adds ClaudeWatcher entries.

### 3. Run ClaudeWatcher

```bash
dotnet run --project src/ClaudeWatcher/ClaudeWatcher.csproj
```

The window stays hidden until your first Claude session starts. Once a session registers, a card appears.

### 4. (Optional) Publish as a standalone exe

```bash
dotnet publish src/ClaudeWatcher/ClaudeWatcher.csproj -c Release -o publish
```

Copy the `publish/` folder to `%LOCALAPPDATA%/ClaudeWatcher/` so the auto-launch hook can find it.

## Usage

### Session Cards

Each active Claude Code session appears as a card showing:

| Element | Description |
|---------|-------------|
| Status indicator | Spinner (working), flashing yellow (waiting), flashing red (error), green dot (idle) |
| Title | Auto-detected from project folder + git branch |
| Message | Last assistant message, truncated to 2 lines |
| Context bar | Thin bar at the bottom showing context fill level |
| Model badge | Shows which Claude model the session is using |
| Elapsed time | How long the session has been running |

**Click a card** to bring its terminal tab to the foreground.

### Right-Click Menu

Right-click anywhere on the watcher window:

- **Recent Sessions** — last 20 sessions with project names. Click one to open a new terminal + Claude in that directory.
- **New Session** — pick a folder, launches Claude there
- **Launch on Startup** — toggle to start ClaudeWatcher with Windows
- **Minimize to Tray** — hides the window, tray icon remains
- **Exit** — shuts down completely

### System Tray

- **Left-click** the tray icon to toggle the window
- The tooltip shows the HTTP server port

### Uninstalling Hooks

```bash
bash scripts/uninstall-hooks.sh
```

Removes only ClaudeWatcher hooks from your settings — everything else is left untouched.

## How It Works

```
Claude Code Session
  │
  ├─ SessionStart hook ──► POST /api/session/start ──► Card appears
  ├─ Stop hook ──────────► POST /api/session/update ──► Card updates
  ├─ Notification hook ──► POST /api/session/notification ──► Status changes
  └─ SessionEnd hook ───► POST /api/session/end ──► Card disappears
                                    │
                              ClaudeWatcher
                          (Kestrel on port 22322)
```

The hooks also set the terminal tab title to `CW:{session_id}` via an OSC escape sequence. When you click a card, ClaudeWatcher uses the Windows UI Automation API to find the tab with that title in Windows Terminal and activate it.

## Architecture

```
src/ClaudeWatcher/
├── App.xaml.cs                 # Startup, single-instance, tray icon
├── MainWindow.xaml             # Borderless dark window, context menu
├── Models/
│   ├── SessionInfo.cs          # Session data model
│   ├── HookPayloads.cs         # JSON payload records
│   ├── AppSettings.cs          # Persisted settings
│   └── RecentSession.cs        # Recent session entry
├── ViewModels/
│   ├── MainViewModel.cs        # Session collection, commands
│   └── SessionCardViewModel.cs # Per-card state, animations, context bar
├── Views/
│   └── SessionCard.xaml        # Card UI with status indicators
├── Services/
│   ├── HookServer.cs           # Kestrel HTTP server
│   ├── SessionManager.cs       # Thread-safe session store
│   ├── TerminalFocusService.cs # UI Automation tab finder
│   ├── RecentSessionsService.cs# Persisted recent sessions
│   ├── SettingsService.cs      # App settings JSON
│   └── StartupService.cs       # Windows startup registry
├── Helpers/
│   ├── GitInfoHelper.cs        # Branch/worktree detection
│   └── ProcessTreeHelper.cs    # PID → parent chain for fallback
└── Resources/
    ├── Theme.xaml               # Dark color palette
    └── Animations.xaml          # Spinner, flash storyboards
```

## Configuration

Settings are stored in `%LOCALAPPDATA%/ClaudeWatcher/settings.json`:

```json
{
  "LaunchOnStartup": false,
  "HttpPort": 22322,
  "WindowOpacity": 0.95,
  "MaxWindowHeight": 600
}
```

Recent sessions are stored in `%LOCALAPPDATA%/ClaudeWatcher/recent-sessions.json`.

## Context Bar Calculation

The context usage bar mirrors Claude Code's own status line calculation:

```
FREE = (CONTEXT_WINDOW - USED - 33000) × 100 / CONTEXT_WINDOW
```

Where 33,000 tokens is a buffer for Claude's response. The bar color changes:
- **Green** — more than 30% free
- **Yellow** — 10–30% free
- **Red** — less than 10% free

## Known Limitations

- **Windows Terminal tab focus** relies on the UI Automation API, which is undocumented for Windows Terminal's internal control types. It works by matching the tab title set via the SessionStart hook. If the title gets overridden by your shell prompt, tab focusing may not work — the window will still be brought to the foreground.
- **VS Code terminal** — clicking a card will focus the VS Code window but cannot activate the specific terminal tab within it.
- **Sessions started before ClaudeWatcher** won't have cards until their next hook event fires (typically the next `Stop` hook after Claude responds).

## License

MIT — see [LICENSE](LICENSE).
