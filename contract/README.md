# State-file contract

The one-way contract between `companion/` (producer) and `dock/` (consumer).
`state-file.schema.json` is the machine-checkable source of truth; this file is the
prose.

## Shape

```json
{
  "schemaVersion": 2,
  "windowId": "f7bae137-7a2f-4dd3-88fc-a09365358854...",
  "folderUri": "vscode-remote://wsl%2Bubuntu-24.04/var/www/project",
  "remoteKind": "wsl",
  "displayName": "project",
  "pid": 41280,
  "lastSeen": "2026-06-18T10:00:00Z"
}
```

## Rules

- **One file per window**, named `{windowId}.json`, in the shared state directory.
- On each heartbeat the companion **bumps the file's mtime** (a cheap touch); it
  rewrites the content only when the reported identity changes (e.g. folder switch).
  Default heartbeat 2s, configurable.
- On clean deactivation the companion **deletes** the file. This is a latency
  optimization only — correctness comes from heartbeat liveness, because `SIGKILL`,
  force-quit, and power loss run no exit code.
- The dock determines liveness from **file mtime**: older than the reap threshold
  (proposed ~6s, tolerating one missed beat) == dead window. `lastSeen` records the
  last *content* change (not each heartbeat), so **mtime — not lastSeen — is the
  freshness signal.**
- The dock's **match key** is `folderUri` (exact comparison). The companion reporting
  the exact remote URI is what makes dedup exact rather than a title-fragment guess.
  **Canonical form is `workspaceFolder.uri.toString()`** — percent-encoded, with the
  authority lowercased (`vscode-remote://wsl%2Bubuntu-24.04/...`). Both sides must use
  this exact string; do NOT mix in `uri.authority` (which preserves case as
  `Ubuntu-24.04`) or `uri.fsPath` (Windows-mangles remote POSIX paths to `\var\www`).

## Field derivation (companion side)

- `folderUri` — `vscode.workspace.workspaceFolders[0].uri.toString()`. Carries the
  `wsl+...` authority even when the extension runs on the UI/Windows host.
- `remoteKind` — from `vscode.env.remoteName` (populated on the UI host): `undefined`
  → `local`, `"wsl"` → `wsl`, `"ssh-remote"` → `ssh`, `"dev-container"` /
  `"attached-container"` → `container`.
- `displayName` — `workspaceFolder.name` (the folder basename; NOT `fsPath`, which
  Windows-mangles remote POSIX paths).
- `pid` — `process.pid`: the extension-host process id on the host the companion runs
  on (Windows, for ui placement). For the dock's focus / window correlation.
- `windowId` — `vscode.env.sessionId`. Unique per window (distinct concurrent windows
  get distinct ids).

## Shared directory

- WSL view:     `/mnt/c/Users/micha/AppData/Local/VsCodeProjectsDock/windows/`
- Windows view: `%LOCALAPPDATA%\VsCodeProjectsDock\windows\`

## Changing the contract

Bump `schemaVersion`, update `state-file.schema.json` and this file, and change both
producer and consumer in the same commit.
