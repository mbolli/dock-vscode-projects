# VS Code Projects Dock

Shows open and favourite VS Code projects as a side-by-side strip in the PowerToys
Command Palette Dock.

## Architecture

Two artifacts with a strict one-way contract between them:

1. **`companion/`** — a VS Code extension that runs inside every window and reports
   that window's identity and liveness by writing a JSON file to a shared directory.
   Built in WSL (TypeScript / Node).
2. **`dock/`** — a PowerToys Command Palette extension (C#) that reads the reported
   state, renders the band, owns favourites, and performs focus/launch actions.
   Built on Windows over `\\wsl.localhost\Ubuntu-24.04\var\www\dock-vscode-projects`
   with the standalone `dotnet` CLI. The project stays in the WSL monorepo; only the
   MSIX *install location* must be local NTFS (WSL's 9P filesystem can't host a
   registered package). See `dock/README.md` for the build/deploy loop.

Data flows one direction only: windows publish state, the dock consumes it. The
companion reports what *is*, never what to do; all behaviour lives in the dock.

## The contract

`contract/state-file.schema.json` is the single source of truth for the JSON each
window writes. Both sides code against it; change it in one atomic commit.

## Transport

File-based shared-state directory. Each window owns `{windowId}.json` and rewrites it
on a heartbeat. The dock treats stale files (mtime older than the reap threshold) as
dead windows. See `contract/README.md`.

- WSL view:     `/mnt/c/Users/micha/AppData/Local/VsCodeProjectsDock/windows/`
- Windows view: `%LOCALAPPDATA%\VsCodeProjectsDock\windows\`

## Releases

Tag-prefixed so the two channels don't collide: `companion-vX.Y.Z` (VS Code
Marketplace) and `dock-vX.Y.Z` (PowerToys / winget).
