# State-file contract

The one-way contract between `companion/` (producer) and `dock/` (consumer).
`state-file.schema.json` is the machine-checkable source of truth; this file is the
prose.

## Shape

```json
{
  "schemaVersion": 1,
  "windowId": "f7bae137-7a2f-4dd3-88fc-a09365358854...",
  "folderUri": "vscode-remote://wsl%2Bubuntu-24.04/var/www/project",
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
  **Canonical form is `workspaceFolder.uri.toString()`** — percent-encoded, with the
  authority lowercased (`vscode-remote://wsl%2Bubuntu-24.04/...`). Both sides must use
  this exact string; do NOT mix in `uri.authority` (which preserves case as
  `Ubuntu-24.04`) or `uri.fsPath` (Windows-mangles remote POSIX paths to `\var\www`).

## Field derivation (companion side, confirmed by Phase 0 probe)

- `folderUri` — `vscode.workspace.workspaceFolders[0].uri.toString()`. Verified to
  carry the `wsl+...` authority even when the extension runs on the UI/Windows host.
- `remoteKind` — from `vscode.env.remoteName` (populated on the UI host): `undefined`
  → `local`, `"wsl"` → `wsl`, `"ssh-remote"` → `ssh`, `"dev-container"` /
  `"attached-container"` → `container`.
- `displayName` — `workspaceFolder.name` (the folder basename; NOT `fsPath`, which
  Windows-mangles remote POSIX paths).
- `windowId` — `vscode.env.sessionId`. Verified unique across two concurrent windows
  (the per-window-vs-per-host risk is settled — distinct windows get distinct ids).

## Shared directory

- WSL view:     `/mnt/c/Users/micha/AppData/Local/VsCodeProjectsDock/windows/`
- Windows view: `%LOCALAPPDATA%\VsCodeProjectsDock\windows\`

## Changing the contract

Bump `schemaVersion`, update `state-file.schema.json` and this file, and change both
producer and consumer in the same commit.
