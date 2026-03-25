---
name: claude-watcher
status: backlog
created: 2026-03-25T17:48:26Z
updated: 2026-03-25T17:48:26Z
progress: 0%
prd: .claude/prds/claude-watcher.md
github:
---

# Epic: claude-watcher

<overview>

Build ClaudeWatcher — a WPF desktop application that monitors active Claude Code sessions via HTTP hooks. The app displays session cards with live status indicators, context usage bars, and message snippets. Clicking a card focuses the correct Windows Terminal tab using UI Automation. The app is borderless, dark-mode, always-on-top, and auto-launches when the first Claude session starts.

</overview>

<architecture-decisions>

<decision id="AD-1" title="Embedded HTTP Server via Kestrel Minimal API">
The watcher embeds an ASP.NET Core Kestrel server using the minimal API pattern. This runs in-process alongside the WPF app. Kestrel binds to `127.0.0.1:22322` and receives JSON payloads from Claude Code's native HTTP hooks.

**Why Kestrel over HttpListener:** Kestrel is the standard .NET HTTP server, handles concurrent requests efficiently, and integrates seamlessly with `System.Text.Json` and dependency injection. `HttpListener` is legacy and requires admin rights for non-localhost prefixes.

**Why port 22322:** Unlikely to conflict with common services. Configurable via settings.
</decision>

<decision id="AD-2" title="UI Automation for Windows Terminal Tab Focus">
Use `System.Windows.Automation` (UI Automation API) to find and activate Windows Terminal tabs by title.

**Strategy:**
1. On session start, the hook sets the terminal tab title to `CW:{session_id_short}` using the OSC escape sequence (`\033]0;CW:abc123\007`) written to `/dev/tty`
2. The watcher stores this title-to-session mapping
3. On card click, enumerate all `CASCADIA_HOSTING_WINDOW_CLASS` windows, search descendants for an element whose `Name` matches the title, invoke `SelectionItemPattern.Select()`, then `SetForegroundWindow()` on the WT HWND

**Why not `wt focus-tab`:** Has a regression (issue #19324) where it opens a new tab instead of focusing. Also requires knowing the tab index, which has no API.

**Why not PID mapping:** No API exists to map a process PID to a Windows Terminal tab index.

**Fallback for non-WT terminals:** Use `SetForegroundWindow()` + `ShowWindow(SW_RESTORE)` via the parent process HWND, found by walking the process tree from the shell PID.
</decision>

<decision id="AD-3" title="MVVM with CommunityToolkit.Mvvm">
Standard MVVM pattern using CommunityToolkit.Mvvm for observable properties, commands, and source generators. Keeps the UI layer thin and testable.
</decision>

<decision id="AD-4" title="Custom Borderless Window with WPF">
Use `WindowStyle="None"`, `AllowsTransparency="True"`, and a custom `Border` with `CornerRadius="8"` for the borderless rounded window. Drag handled via `MouseLeftButtonDown` → `DragMove()`. No WindowChrome — fully custom.

**Why not WindowChrome:** WindowChrome still renders a title bar area and doesn't support rounded corners natively. Full custom gives complete control over the look.
</decision>

<decision id="AD-5" title="Session State is In-Memory Only">
Active sessions are stored in a `ConcurrentDictionary<string, SessionInfo>` in the HTTP server. No database, no file persistence for active sessions. Recent sessions (for the right-click menu) are persisted to a JSON file in `%LOCALAPPDATA%/ClaudeWatcher/recent-sessions.json`.

**Why in-memory:** Sessions are transient. If the watcher restarts, sessions re-register via their next hook call. Simplicity over durability.
</decision>

<decision id="AD-6" title="Hook Installation Strategy">
Hooks are added to `~/.claude/settings.json` by a setup script or installer. The hooks use a mix of HTTP type (for data posting) and command type (for context injection and tab title setting).

**Hook configuration:**
```json
{
  "hooks": {
    "SessionStart": [
      {
        "matcher": "",
        "hooks": [
          {
            "type": "command",
            "command": "bash -c 'input=$(cat); sid=$(echo \"$input\" | grep -o '\"session_id\":\"[^\"]*\"' | head -1 | cut -d'\"' -f4); short=${sid:0:8}; echo -ne \"\\033]0;CW:${short}\\007\" > /dev/tty 2>/dev/null; curl -sf http://127.0.0.1:22322/api/health > /dev/null 2>&1 || (start \"\" \"%LOCALAPPDATA%/ClaudeWatcher/ClaudeWatcher.exe\" 2>/dev/null; sleep 2); curl -sf -X POST http://127.0.0.1:22322/api/session/start -H \"Content-Type: application/json\" -d \"$input\" > /dev/null 2>&1; echo \"{\\\"additionalContext\\\": \\\"ClaudeWatcher is monitoring this session. Focus on your work as normal.\\\"}\"'",
            "timeout": 10
          }
        ]
      }
    ],
    "Stop": [
      {
        "matcher": "",
        "hooks": [
          {
            "type": "command",
            "command": "bash -c 'input=$(cat); curl -sf -X POST http://127.0.0.1:22322/api/session/update -H \"Content-Type: application/json\" -d \"$input\" > /dev/null 2>&1; exit 0'",
            "timeout": 5
          }
        ]
      }
    ],
    "Notification": [
      {
        "matcher": "",
        "hooks": [
          {
            "type": "command",
            "command": "bash -c 'input=$(cat); curl -sf -X POST http://127.0.0.1:22322/api/session/notification -H \"Content-Type: application/json\" -d \"$input\" > /dev/null 2>&1; exit 0'",
            "timeout": 5
          }
        ]
      }
    ],
    "SessionEnd": [
      {
        "matcher": "",
        "hooks": [
          {
            "type": "command",
            "command": "bash -c 'input=$(cat); curl -sf -X POST http://127.0.0.1:22322/api/session/end -H \"Content-Type: application/json\" -d \"$input\" > /dev/null 2>&1; exit 0'",
            "timeout": 5
          }
        ]
      }
    ]
  }
}
```

**Why command hooks instead of HTTP type for all:** The `SessionStart` hook needs to both POST data AND return `additionalContext` JSON AND set the tab title — only command hooks can do all three. Other hooks could use HTTP type, but command hooks with curl give consistent error suppression via `2>/dev/null` and `exit 0`.
</decision>

</architecture-decisions>

<technical-approach>

<wpf-app>

**Project structure:**
```
ClaudeWatcher/
├── ClaudeWatcher.sln
├── src/
│   └── ClaudeWatcher/
│       ├── ClaudeWatcher.csproj          (.NET 10, WPF)
│       ├── App.xaml / App.xaml.cs         (startup, single-instance, tray)
│       ├── MainWindow.xaml / .cs          (borderless, draggable, card list)
│       ├── Models/
│       │   ├── SessionInfo.cs            (session data model)
│       │   └── RecentSession.cs          (persisted recent session)
│       ├── ViewModels/
│       │   ├── MainViewModel.cs          (session list, visibility logic)
│       │   └── SessionCardViewModel.cs   (per-card state, animations)
│       ├── Views/
│       │   └── SessionCard.xaml / .cs    (card UserControl)
│       ├── Services/
│       │   ├── HookServer.cs             (Kestrel minimal API, endpoints)
│       │   ├── SessionManager.cs         (ConcurrentDictionary, lifecycle)
│       │   ├── TerminalFocusService.cs   (UI Automation, SetForegroundWindow)
│       │   ├── RecentSessionsService.cs  (JSON persistence, launch)
│       │   └── StartupService.cs         (Windows startup shortcut)
│       ├── Helpers/
│       │   ├── ProcessTreeHelper.cs      (PID → parent chain)
│       │   └── GitInfoHelper.cs          (branch, worktree detection)
│       └── Resources/
│           ├── Theme.xaml                (dark theme, colors, styles)
│           └── Animations.xaml           (spinner, pulse, flash)
├── scripts/
│   ├── install-hooks.sh                  (adds hooks to ~/.claude/settings.json)
│   └── uninstall-hooks.sh               (removes hooks)
└── .gitignore
```

</wpf-app>

<http-server>

The HTTP server runs on a background thread, started in `App.OnStartup()`. Uses ASP.NET Core minimal API:

```csharp
var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:22322");
var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok());
app.MapPost("/api/session/start", (SessionStartPayload p) => { ... });
app.MapPost("/api/session/update", (SessionUpdatePayload p) => { ... });
app.MapPost("/api/session/notification", (SessionNotificationPayload p) => { ... });
app.MapPost("/api/session/end", (SessionEndPayload p) => { ... });

app.RunAsync(); // non-blocking
```

Each endpoint updates the `SessionManager`, which raises events consumed by the ViewModel on the UI thread via `Dispatcher.InvokeAsync`.

</http-server>

<terminal-focus>

**Tab title strategy:**
1. SessionStart hook sets tab title to `CW:{session_id_short}` via OSC escape to `/dev/tty`
2. Watcher stores mapping: `session_id → "CW:{short_id}"`
3. On click: UI Automation searches all WT windows for a tab element with matching `Name`

**Process tree fallback (non-WT terminals):**
1. From `SessionStartPayload`, extract the shell PID (parent of the hook process)
2. Walk the process tree to find the terminal window
3. Use `SetForegroundWindow()` + `ShowWindow(SW_RESTORE)`

**Focus-stealing workaround:**
Use `AttachThreadInput()` to attach to the foreground thread before calling `SetForegroundWindow()`, then detach. This bypasses Windows' focus-stealing prevention.

</terminal-focus>

<card-ui>

Each card is a `UserControl` with:
- `Border` with `CornerRadius="6"`, dark background (`#1E1E2E`)
- Status indicator: `Ellipse` with animations bound to `SessionStatus` enum
  - Working: `RotateTransform` on a circular arc (spinner)
  - Waiting: `DoubleAnimation` on `Opacity` (flashing yellow)
  - Error: `DoubleAnimation` on `Opacity` (flashing red)
  - Idle: solid green `Ellipse`
- Title: `TextBlock` with `FontWeight="SemiBold"`
- Message: `TextBlock` with `MaxHeight` and `TextTrimming="CharacterEllipsis"`
- Context bar: `Rectangle` in the card background with `Width` bound to percentage, gradient fill
- Time: `TextBlock` with a `DispatcherTimer` updating elapsed time every second
- Model badge: small `Border` with rounded corners and model name text

</card-ui>

<context-bar>

The context usage bar is rendered as a subtle horizontal gradient at the bottom of each card:
- Width = `(used_percentage / 100) * card_width`
- Color: green (#4CAF50 at 20% opacity) when >30% free, yellow (#FFB800 at 25% opacity) when 10-30% free, red (#FF4444 at 30% opacity) when <10% free
- Calculation mirrors the status-line.sh script:
  ```
  FREE = (CTX - USED - BUFFER) * 100 / CTX
  where BUFFER = 33000, USED = CTX * used_percentage / 100
  ```

</context-bar>

</technical-approach>

<implementation-strategy>

Build in vertical slices — each task delivers a working increment:

1. **Skeleton app** — Borderless dark window, can be dragged, minimized to tray, single-instance
2. **HTTP server** — Kestrel server with endpoints, accepts JSON, stores sessions in memory
3. **Session cards** — Card UI with status indicators, title, message, context bar, animations
4. **Hook scripts** — Install/uninstall scripts, hook configuration, tab title setting
5. **Terminal focus** — UI Automation tab finder, process tree fallback, focus-stealing workaround
6. **Right-click menu** — Context menu with recent sessions, new session, settings, minimize
7. **Settings & startup** — Persistent settings, Windows startup shortcut, port config
8. **Polish** — Orphan detection, error handling, smooth animations, edge cases

</implementation-strategy>

<task-breakdown-preview>

| # | Task | Parallel | Depends On | Effort |
|---|------|----------|------------|--------|
| 1 | WPF skeleton: borderless dark window, drag, single-instance, tray icon | No | — | M |
| 2 | Embedded Kestrel HTTP server with session endpoints | Yes (with 1) | — | M |
| 3 | Session card UI: status indicators, title, message, context bar, animations | No | 1 | L |
| 4 | Wire HTTP server → SessionManager → ViewModel → Cards | No | 2, 3 | M |
| 5 | Hook scripts: install/uninstall, SessionStart with tab title + auto-launch | No | 2 | M |
| 6 | Terminal tab focus via UI Automation + process tree fallback | No | 4 | L |
| 7 | Right-click context menu: recent sessions, new session, settings | No | 4 | M |
| 8 | Settings persistence and Windows startup shortcut | No | 7 | S |

S = Small (1-2 files), M = Medium (3-5 files), L = Large (5+ files or complex logic)

</task-breakdown-preview>

<dependencies>

- .NET 10 SDK (net10.0-windows TFM)
- Microsoft.AspNetCore.App (Kestrel, minimal API — included in .NET SDK)
- CommunityToolkit.Mvvm (MVVM source generators)
- H.NotifyIcon.Wpf (system tray icon)
- System.Text.Json (JSON serialization — included in .NET)
- UIAutomationClient + UIAutomationTypes (UI Automation — included in Windows)

</dependencies>

<success-criteria-technical>

1. `dotnet build` succeeds with zero warnings on .NET 10
2. HTTP server responds to POST `/api/session/start` within 50ms
3. Card appears within 500ms of session registration
4. UI Automation correctly finds and activates a Windows Terminal tab by title
5. Hook scripts install without conflicting with existing user hooks
6. Memory usage stays under 80MB with 10 active sessions
7. App starts in under 2 seconds
8. Zero unhandled exceptions in normal operation

</success-criteria-technical>

<estimated-effort>

8 tasks, approximately 5-7 days of focused implementation. Tasks 1 and 2 can run in parallel. Task 6 (terminal focus) is the highest-risk item due to undocumented UI Automation tree structure in Windows Terminal — budget extra time for prototyping and testing across WT versions.

</estimated-effort>

<tasks-created>

- [ ] 001.md - WPF skeleton: borderless dark window, drag, single-instance, tray icon (parallel: true)
- [ ] 002.md - Embedded Kestrel HTTP server with session endpoints (parallel: true)
- [ ] 003.md - Session card UI: status indicators, context bar, animations (parallel: false, depends: 001)
- [ ] 004.md - Wire HTTP server to SessionManager to ViewModel to Cards (parallel: false, depends: 002, 003)
- [ ] 005.md - Hook scripts: install/uninstall, tab title, auto-launch (parallel: false, depends: 002)
- [ ] 006.md - Terminal tab focus via UI Automation + process tree fallback (parallel: false, depends: 004)
- [ ] 007.md - Right-click context menu: recent sessions, new session, settings (parallel: false, depends: 004)
- [ ] 008.md - Settings persistence and Windows startup shortcut (parallel: false, depends: 007)

Total tasks: 8
Parallel tasks: 2 (001, 002 can run simultaneously)
Sequential tasks: 6
Estimated total effort: 37-57 hours

**Dependency graph:**
```
001 ──┐
      ├──> 003 ──┐
002 ──┤          ├──> 004 ──┬──> 006
      │          │          ├──> 007 ──> 008
      └──> 005   │
                 │
```

</tasks-created>
