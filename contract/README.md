# State-file contract

The one-way contract between `companion/` (producer) and `dock/` (consumer).
`state-file.schema.json` is the machine-checkable source of truth; this file is the
prose.

## Shape

```json
{
  "schemaVersion": 1,
  "windowId": "<stable per-window id>",
  "folderUri": "vscode-remote://wsl+Ubuntu/home/michael/project",
  "remoteKind": "wsl",
  "displayName": "project",
  "lastSeen": "2026-06-18T10:00:00Z"
}
```

## Rules

- **One file per window**, named `{windowId}.json`, in the shared state directory.
- The companion **rewrites** the file on each heartbeat (proposed 2s).
- On clean deactivation the companion **deletes** the file. This is a latency
  optimization only — correctness comes from heartbeat liveness, because `SIGKILL`,
  force-quit, and power loss run no exit code.
- The dock determines liveness from **file mtime**: older than the reap threshold
  (proposed ~6s, tolerating one missed beat) == dead window. `lastSeen` is redundant
  with mtime and exists so the payload is self-describing.
- The dock's **match key** is `folderUri` (exact comparison). The companion reporting
  the exact remote URI is what makes dedup exact rather than a title-fragment guess.

## Field derivation (companion side)

- `folderUri` — from `vscode.workspace.workspaceFolders`.
- `remoteKind` — from the URI scheme/authority: `wsl+` → `wsl`, `dev-container+` →
  `container`, `ssh-remote+` → `ssh`, otherwise `local`.
- `displayName` — leaf of the folder path.
- `windowId` — Phase 0 (Group 1) confirms the source.

## Shared directory

- WSL view:     `/mnt/c/Users/micha/AppData/Local/VsCodeProjectsDock/windows/`
- Windows view: `%LOCALAPPDATA%\VsCodeProjectsDock\windows\`

## Changing the contract

Bump `schemaVersion`, update `state-file.schema.json` and this file, and change both
producer and consumer in the same commit.
