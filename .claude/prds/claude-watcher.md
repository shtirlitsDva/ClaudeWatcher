---
name: claude-watcher
description: Dark-mode always-on-top desktop app that monitors active Claude Code sessions via native hooks
status: active
created: 2026-03-25T17:48:26Z
---

# PRD: claude-watcher

<executive-summary>

ClaudeWatcher is a lightweight, always-on-top WPF desktop application that gives users at-a-glance visibility into all active Claude Code sessions. Each session appears as a card with a live status indicator, session title, latest message snippet, and a context usage bar. Clicking a card brings the correct terminal tab to the foreground — even inside Windows Terminal. All communication uses Claude Code's native HTTP hook system: no file polling, no custom IPC protocols.

The app targets power users who run multiple Claude Code sessions simultaneously across different projects and need to quickly see which sessions need attention, which are working, and which have finished — then jump to the right one in a single click.

</executive-summary>

<problem-statement>

When running multiple Claude Code sessions across projects, the user has no centralized view of what each agent is doing. They must manually switch between terminal tabs to check status, and have no way to know which session needs attention without visiting each one. Windows Terminal's tab bar shows generic titles, making it hard to find the right session. This friction grows linearly with the number of active sessions.

</problem-statement>

<user-stories>

<story id="1">
**As a** multi-session Claude user,
**I want to** see all my active Claude sessions in one floating window,
**so that** I can monitor their progress without switching terminals.

**Acceptance criteria:**
- Each session appears as a card in the watcher window
- Cards show in the order sessions were started (newest on top)
- The window floats above all other windows
- The window is invisible until the first session registers
</story>

<story id="2">
**As a** user waiting on Claude,
**I want to** see clear visual indicators of each session's status,
**so that** I know at a glance which sessions need my attention.

**Acceptance criteria:**
- Working: animated spinner on the card
- Waiting for input: flashing yellow indicator
- Error: flashing red indicator
- Finished work (idle): solid green indicator
- Card disappears when session closes (terminal closed)
</story>

<story id="3">
**As a** user monitoring session context usage,
**I want to** see how much context each session has consumed,
**so that** I know which sessions are running low and might need compaction.

**Acceptance criteria:**
- A gradient bar spans the card background showing context fill level
- Color transitions: green (>30% free) → yellow (10-30% free) → red (<10% free)
- Context percentage calculated as: FREE = (CTX - USED - BUFFER) * 100 / CTX, where BUFFER = 33000 tokens
</story>

<story id="4">
**As a** user who wants to focus a session,
**I want to** click a card and have the exact terminal tab brought to the foreground,
**so that** I don't have to manually search through Windows Terminal tabs.

**Acceptance criteria:**
- Clicking a card brings the correct Windows Terminal window to the foreground
- The correct tab within Windows Terminal is activated (not just the window)
- Works even when the terminal is minimized
- Works for sessions in separate terminal windows (not just tabs)
</story>

<story id="5">
**As a** user who wants meaningful session names,
**I want** Claude to set descriptive session titles that update as work progresses,
**so that** I can tell what each session is doing from the card title alone.

**Acceptance criteria:**
- On session start, Claude receives context injection telling it how to set/update titles
- Default title when Claude hasn't set one: "ProjectName - Branch" (worktree name appended if in a worktree)
- Title updates are sent via hook calls to the watcher's HTTP API
</story>

<story id="6">
**As a** returning user,
**I want to** quickly resume recent sessions or start new ones from the watcher,
**so that** I don't have to navigate to project folders manually.

**Acceptance criteria:**
- Right-click the watcher window → "Recent Sessions" shows last 10 sessions with project name, folder, and time
- Clicking a recent session opens a new terminal in that directory and starts Claude
- "New Session" option opens a folder picker, then starts Claude in that folder
</story>

<story id="7">
**As a** daily user,
**I want to** configure ClaudeWatcher to start with Windows,
**so that** it's always ready when I start working.

**Acceptance criteria:**
- Right-click → Settings → "Launch on Windows startup" toggle
- Uses Windows Startup folder shortcut (not Task Scheduler)
- Setting persists across app restarts
</story>

<story id="8">
**As a** user who values screen real estate,
**I want** the watcher window to be minimal and movable,
**so that** it doesn't get in the way of my work.

**Acceptance criteria:**
- Borderless window with rounded corners
- Draggable by clicking and holding anywhere on the surface
- Resizes vertically based on number of cards (auto-height)
- Has a maximum height with scrolling when many sessions are active
- Minimizes to system tray via right-click → "Minimize to Tray"
- Tray icon left-click toggles visibility
- Invisible when no sessions are active (no empty state window)
</story>

</user-stories>

<functional-requirements>

<req id="FR-1" title="HTTP Hook Server">
ClaudeWatcher runs a local HTTP server (default port: 22322) that receives JSON payloads from Claude Code's native HTTP hooks.

**Endpoints:**
- `POST /api/session/start` — Register a new session
- `POST /api/session/update` — Update session status, title, message, or context usage
- `POST /api/session/notification` — Flag session as needing attention
- `POST /api/session/end` — Remove session card
- `GET /api/health` — Health check (used by hooks to verify watcher is running)

**Payload schemas:**

Start:
```json
{
  "session_id": "abc123",
  "cwd": "C:/Users/dev/project",
  "model": "claude-opus-4-6",
  "timestamp": "2026-03-25T14:30:00Z"
}
```

Update:
```json
{
  "session_id": "abc123",
  "title": "Fixing auth middleware",
  "message": "Reading src/auth.ts to understand the current flow",
  "status": "working",
  "context_used_percentage": 42,
  "context_window_size": 200000,
  "timestamp": "2026-03-25T14:35:00Z"
}
```

Notification:
```json
{
  "session_id": "abc123",
  "notification_type": "permission_prompt",
  "message": "Claude Code needs permission to use Bash",
  "timestamp": "2026-03-25T14:36:00Z"
}
```

End:
```json
{
  "session_id": "abc123",
  "timestamp": "2026-03-25T15:00:00Z"
}
```
</req>

<req id="FR-2" title="Hook Configuration">
The watcher's hook configuration is installed in the user's `~/.claude/settings.json`. Hooks use the native `"type": "http"` hook format. The configuration must be installable via a setup command or script.

**Required hooks:**
- `SessionStart` → POST to `/api/session/start` (also injects context via a companion command hook)
- `Stop` → POST to `/api/session/update` with latest message and context stats
- `Notification` → POST to `/api/session/notification`
- `SessionEnd` → POST to `/api/session/end`

**Context injection hook (command type):**
A `SessionStart` command hook that outputs `additionalContext` JSON telling Claude:
- How to set session titles via a specific tool call pattern or message format
- That it should update the title as work progresses to reflect current focus

**Auto-launch:**
The `SessionStart` hook script checks if the watcher is running (GET `/api/health`). If not, it launches `ClaudeWatcher.exe` and waits briefly for it to start before proceeding.

**Error suppression:**
All hooks must suppress errors so they never pollute the Claude terminal. HTTP hooks that fail (watcher not running) should fail silently. Command hooks redirect stderr to `/dev/null`.
</req>

<req id="FR-3" title="Session Cards">
Each active session is displayed as a card in the watcher window.

**Card elements:**
- **Status indicator** (left side): animated spinner (working), flashing yellow dot (waiting), flashing red dot (error), solid green dot (finished/idle)
- **Title** (top): session title set by Claude, or default "ProjectName - Branch [- WorktreeName]"
- **Message snippet** (middle): last assistant message, truncated to 2 lines
- **Context usage bar** (card background): horizontal gradient bar showing context fill percentage
- **Elapsed time** (bottom-right): time since session started, updating live (e.g., "12m", "1h 23m")
- **Model badge** (top-right): small badge showing the model name (e.g., "Opus 4.6")
</req>

<req id="FR-4" title="Terminal Tab Focus">
Clicking a card brings the associated terminal to the foreground and activates the correct tab.

**Implementation approach:**
- On session start, record the parent process ID (the shell process hosting Claude)
- Trace the process tree upward to find the Windows Terminal process
- Use the **UI Automation API** to enumerate Windows Terminal tabs
- Match the correct tab by comparing its associated process or by tab title matching
- Use `SetFocus()` via UI Automation to activate the specific tab
- Fall back to `SetForegroundWindow()` + `ShowWindow(SW_RESTORE)` for non-tabbed terminals

**Supported terminals:**
- Windows Terminal (tabs + windows)
- Standalone console windows (cmd, PowerShell, Git Bash)
- VS Code integrated terminal (best effort — may only focus the VS Code window)
</req>

<req id="FR-5" title="Right-Click Context Menu">
Right-clicking anywhere on the watcher window shows a context menu:

- **Recent Sessions** → submenu listing last 10 sessions (project name, folder path, timestamp)
  - Clicking an entry opens a new Windows Terminal tab in that directory and launches `claude`
- **New Session** → opens a folder picker dialog, then launches Claude in the selected folder
- **Settings** → opens settings panel
  - "Launch on Windows startup" toggle
  - HTTP port configuration
  - Appearance settings (opacity, max height)
- **Minimize to Tray** → hides window, shows tray icon
- **Exit** → shuts down the watcher completely
</req>

<req id="FR-6" title="System Tray">
When minimized to tray:
- Tray icon shows a small status badge (green if all sessions idle, yellow if any waiting, red if any errors)
- Left-click tray icon toggles window visibility
- Right-click tray icon shows same context menu as the main window
- Tooltip shows "ClaudeWatcher — N active sessions"
</req>

<req id="FR-7" title="Session Lifecycle">
- **Registration**: Session card appears when `SessionStart` hook fires
- **Updates**: Card updates on each `Stop` hook (Claude finished a response) with latest message, status, and context usage
- **Attention**: Card flashes when `Notification` hook fires (permission prompt, error)
- **End**: Card removed when `SessionEnd` hook fires
- **Orphan detection**: If no update received for 5 minutes and status is "working", show "Session may be stalled" with dimmed styling. If no update for 30 minutes, auto-remove the card.
- **Startup recovery**: On watcher startup, any existing Claude sessions won't have cards (hooks only fire going forward). This is acceptable — the watcher shows "No active sessions" until the next session starts or an existing one sends an update.
</req>

<req id="FR-8" title="Session Title Management">
**Default title derivation (when Claude hasn't set a custom title):**
Parse from the session's `cwd`:
1. Extract project folder name (last path segment)
2. Query git for current branch: `git -C <cwd> branch --show-current`
3. Detect worktree: compare `git rev-parse --git-dir` vs `--git-common-dir`
4. Format: "ProjectName - branch" or "ProjectName - parentBranch - worktreeName"

**Custom title via Claude:**
Claude can update the session title by calling the watcher API. The `SessionStart` context injection tells Claude:
"You are being monitored by ClaudeWatcher. To set your session title, the Stop hook will send your latest status. Focus on your work — titles are optional but helpful for the user."

The `Stop` hook extracts the `last_assistant_message` and sends it as the message snippet. Claude can also explicitly set a title by including a specific pattern in its response that the hook parses, or the user can configure a `UserPromptSubmit` hook that tracks the conversation topic.

In practice, the most reliable approach is:
- Title = default (project/branch) unless explicitly updated
- Message snippet = truncated last assistant message (always updated via Stop hook)
- This gives enough context without requiring Claude to "know" about the watcher
</req>

</functional-requirements>

<non-functional-requirements>

<nfr id="NFR-1" title="Performance">
- HTTP server must respond within 50ms to hook requests
- UI updates must not block the HTTP server
- Memory usage under 100MB with 20 active sessions
- Startup time under 2 seconds
</nfr>

<nfr id="NFR-2" title="Visual Design">
- Complete dark mode — no light elements
- Borderless window with rounded corners (8px radius)
- Semi-transparent background (opacity 0.92-0.95)
- Smooth animations for status transitions (fade, pulse)
- Modern sans-serif font (Segoe UI Variable or Inter)
- Accent colors: yellow (#FFB800) for waiting, red (#FF4444) for error, green (#4CAF50) for idle, blue (#2196F3) for spinner
- Card background gradient for context usage: subtle, not overwhelming — e.g., a thin bar at the bottom of the card
- No Windows chrome, no title bar, no borders — just content
</nfr>

<nfr id="NFR-3" title="Reliability">
- Hooks must never crash or pollute the Claude terminal
- Watcher crash must not affect running Claude sessions
- Graceful handling of malformed hook payloads
- Auto-recovery if HTTP server port is in use (try next port, update config)
</nfr>

<nfr id="NFR-4" title="Security">
- HTTP server binds only to localhost (127.0.0.1)
- No authentication required (localhost-only access)
- No sensitive data stored (session data is transient, in-memory only)
- Recent sessions list stored in local app data folder
</nfr>

</non-functional-requirements>

<success-criteria>

1. User can start 3+ Claude Code sessions and see all of them as cards in the watcher within 2 seconds of each session starting
2. Clicking any card brings the correct terminal tab to the foreground, including tabs within Windows Terminal
3. Status indicators correctly reflect session state (working, waiting, error, idle)
4. Context usage bar accurately reflects remaining context for each session
5. Right-click → Recent Sessions shows previously closed sessions and can launch new sessions from them
6. Hook errors never appear in the Claude Code terminal output
7. Watcher auto-launches when the first Claude session starts (if not already running)

</success-criteria>

<constraints-and-assumptions>

**Constraints:**
- Windows only (Windows 10/11) — no cross-platform requirement
- .NET 10 with WPF for the desktop app
- Must work with Claude Code's existing hook system — no modifications to Claude Code itself
- HTTP hooks are the primary IPC mechanism
- Must coexist with any existing user hooks in `~/.claude/settings.json`

**Assumptions:**
- User has Claude Code CLI installed and configured
- User has `gh` CLI available for any GitHub operations
- Windows Terminal is the primary terminal (but standalone windows are also supported)
- User runs Claude Code from Git repositories (for branch/worktree detection)
- Claude Code's HTTP hook type supports POST with JSON body to localhost

**Technology decisions:**
- **WPF over Avalonia/MAUI**: WPF was chosen over Avalonia and MAUI for these reasons:
  - MAUI is mobile/tablet focused and has poor support for always-on-top utility windows, system tray, and custom window chrome
  - Avalonia would work but adds cross-platform abstraction overhead for a Windows-only app, and its ecosystem is smaller (fewer UI component libraries, less documentation for advanced Win32 interop)
  - WPF is battle-tested for exactly this type of Windows utility app: borderless windows, system tray integration, Win32 interop for window management, and UI Automation API access are all first-class capabilities
  - Modern WPF with custom XAML styles achieves the same visual quality as Avalonia's Fluent theme
  - .NET 10 WPF gets continued investment from Microsoft

</constraints-and-assumptions>

<out-of-scope>

- Cross-platform support (macOS, Linux)
- Sending commands to Claude sessions from the watcher (no bidirectional communication in v1)
- Streaming Claude's output in the watcher (just status + last message snippet)
- Multi-machine session aggregation
- Authentication or multi-user support
- Plugin/extension system
- Detailed session history or logging beyond recent sessions list

</out-of-scope>

<dependencies>

- **Claude Code CLI** with hook support (HTTP hook type)
- **.NET 10 SDK** for building the WPF app
- **Windows 10/11** for UI Automation API and Win32 interop
- **CommunityToolkit.Mvvm** for MVVM pattern
- **H.NotifyIcon.Wpf** for system tray integration
- **Microsoft.AspNetCore** (minimal API) for the embedded HTTP server
- **System.Text.Json** for JSON serialization

</dependencies>
