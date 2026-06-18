# Phase 0 Probe (throwaway)

Disposable extension to settle two blocking questions before building the real
companion. Delete this whole directory once Phase 0 is done.

- **Q1 — placement:** does a `"ui"`-kind extension (runs on the Windows host) see the
  `wsl+Ubuntu` authority in `workspace.workspaceFolders`? If yes → commit to `"ui"`.
- **Q2 — windowId:** which `vscode.env` value is unique-per-window and stable for the
  window's lifetime? `sessionId` is the prime candidate; `machineId` is per-machine
  (not per-window).

## Run it

```fish
cd phase0-probe
npm install
```

Then in VS Code: open the `phase0-probe` folder and press **F5** ("Run Phase 0
Probe"). An Extension Development Host window opens and the **Dock Phase 0 Probe**
output channel prints the identity dump on startup.

## What to do

1. In the dev-host window, **open a folder that lives inside WSL**.
2. Run command **"Dock Probe: Dump window identity again"** (Ctrl+Shift+P).
3. Read the output channel:
   - `extension.extensionKind` → `1` means it ran on the **UI/Windows** host.
   - The folder's `authority` → if it shows `wsl+Ubuntu`, **Q1 is YES** (UI host sees
     the remote authority). If `scheme` is `file` / authority empty with a host-local
     path, Q1 is NO → fall back to `"workspace"` placement.
   - Note `env.sessionId` across **two concurrent windows** to confirm it differs
     per-window (Q2).
4. To confirm the shared dir is writable from wherever this runs, run **"Dock Probe:
   Write a test file to the shared dir"** and check
   `%LOCALAPPDATA%\VsCodeProjectsDock\windows\`.

## Comparing placements

To test the fallback, change `"extensionKind"` in `package.json` from `["ui"]` to
`["workspace"]`, recompile, and re-run. Compare the logged `extensionKind`,
`process.platform`, and folder `authority`.

## Record the answers

Write Q1/Q2 outcomes back into the kickoff plan, then delete this directory.
