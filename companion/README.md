# companion/ — VS Code reporter extension

Silent reporter. Runs on the **UI (Windows) host** (`extensionKind: ["ui"]`, confirmed
by the Phase 0 probe) and publishes this window's identity + liveness to the shared
state directory the dock reads. It reports what *is*, never what to do.

## What it writes

One file per window, `{windowId}.json`, per `../contract/`:

- `windowId` = `vscode.env.sessionId` (verified unique per window).
- `folderUri` = `workspaceFolder.uri.toString()` (canonical match key).
- `remoteKind` from `vscode.env.remoteName`.
- `displayName` = `workspaceFolder.name`.
- `lastSeen` rewritten every 2s (heartbeat). Atomic temp-then-rename, so the dock
  never reads a torn file.

On clean deactivation the file is deleted (latency optimization). Crashes are handled
by the dock's mtime staleness reap, not by this extension.

Shared dir: `%LOCALAPPDATA%\VsCodeProjectsDock\windows\`
(WSL view `/mnt/c/Users/micha/AppData/Local/VsCodeProjectsDock/windows/`).

## Settings

| Setting | Default | Scope | Meaning |
| --- | --- | --- | --- |
| `vscodeProjectsDock.heartbeatIntervalMs` | `2000` | application | How often the state file is rewritten (ms, min 500). Lower = snappier dock, more writes. |
| `vscodeProjectsDock.sharedDirectory` | `""` (→ `%LOCALAPPDATA%\VsCodeProjectsDock\windows`) | machine | Absolute path to the shared state dir. **The dock must read the same path.** |

Both apply live — no reload. Changing the interval reschedules the heartbeat;
changing the directory deletes the old file and republishes to the new one.

The **reap/staleness threshold lives on the dock, not here** — it's the consumer's
call how long without a heartbeat counts as dead (keep it a small multiple of the
interval).

## Dev

```fish
cd companion
npm install
npm run compile
```

F5 ("Run Companion") launches an Extension Development Host. Or install the built
VSIX (`npm run package`) — installing a `"ui"` extension from a WSL window routes it to
the local host. Run "VS Code Projects Dock: Show companion status" to dump the current
state and file path.

> `publisher` (`michael-bolli`) is the Marketplace publisher ID; the human-readable
> identity is in `author`. v1 reports `workspaceFolders[0]` only (one project per window).
