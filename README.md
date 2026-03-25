# ClaudeWatcher

A dark-mode, always-on-top desktop app that monitors active Claude Code sessions in real time.

## What It Does

- Shows each active Claude Code session as a card with live status indicators
- Spinner while Claude is working, flashing yellow when waiting for input, green when idle
- Displays session title, latest message snippet, and context usage bar
- Click a card to instantly focus that terminal tab — even inside Windows Terminal
- All data flows through Claude Code's native hook system (HTTP hooks)
- Right-click menu for recent sessions, new sessions, and startup settings

## How It Works

ClaudeWatcher runs a lightweight local HTTP server. Claude Code hooks POST session events (start, stop, status changes, notifications) directly to the watcher. No file polling, no custom IPC — just native hooks.

## Tech Stack

- **WPF** (.NET 10) — borderless window, rounded corners, custom dark theme
- **Local HTTP server** — receives hook events from Claude Code
- **UI Automation API** — focuses the correct Windows Terminal tab

## Status

Under active development. See `.claude/prds/claude-watcher.md` for the full product requirements.

## License

MIT
