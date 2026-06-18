import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";

// Contract: see ../contract/state-file.schema.json (single source of truth).
const SCHEMA_VERSION = 2;
const CONFIG_SECTION = "vscodeProjectsDock";
const DEFAULT_HEARTBEAT_MS = 2000;
const MIN_HEARTBEAT_MS = 500;

type RemoteKind = "local" | "wsl" | "container" | "ssh";

interface WindowState {
  schemaVersion: number;
  windowId: string;
  folderUri: string;
  remoteKind: RemoteKind;
  displayName: string;
  pid: number;
  lastSeen: string;
}

// Identity = the fields whose change forces a full rewrite. While they are stable, a
// heartbeat is just a cheap mtime touch.
type Identity = Pick<
  WindowState,
  "windowId" | "folderUri" | "remoteKind" | "displayName" | "pid"
>;

let log: vscode.OutputChannel;
let heartbeat: ReturnType<typeof setInterval> | undefined;
let lastWritten: string | undefined; // path of the file we currently own, if any
let lastSignature: string | undefined; // JSON of the last-written identity
let dirEnsured = false; // mkdir already done for the current shared dir

function config(): vscode.WorkspaceConfiguration {
  return vscode.workspace.getConfiguration(CONFIG_SECTION);
}

// Clamped so a typo in settings can't turn the heartbeat into a busy loop.
function heartbeatMs(): number {
  const v = config().get<number>("heartbeatIntervalMs", DEFAULT_HEARTBEAT_MS);
  return Math.max(MIN_HEARTBEAT_MS, v);
}

// Override via setting, else %LOCALAPPDATA%\VsCodeProjectsDock\windows\ — present
// because Phase 0 fixed this to the ui (Windows) host. The /mnt/c fallback only
// matters if placement ever changes. The dock must read the same directory.
function sharedDir(): string {
  const override = config().get<string>("sharedDirectory", "").trim();
  if (override) {
    return override;
  }
  const localAppData = process.env.LOCALAPPDATA;
  if (localAppData) {
    return path.join(localAppData, "VsCodeProjectsDock", "windows");
  }
  return "/mnt/c/Users/micha/AppData/Local/VsCodeProjectsDock/windows";
}

// windowId is the filename key. sessionId is a GUID + timestamp; sanitize defensively.
function stateFilePath(): string {
  const safe = vscode.env.sessionId.replace(/[^A-Za-z0-9._-]/g, "_");
  return path.join(sharedDir(), `${safe}.json`);
}

function remoteKind(): RemoteKind {
  const r = vscode.env.remoteName; // populated on the UI host (Phase 0 confirmed)
  if (!r) return "local";
  if (r === "wsl") return "wsl";
  if (r === "ssh-remote") return "ssh";
  if (r.includes("container")) return "container";
  return "local";
}

// null when no folder is open: the dock only tracks windows that have a project.
function computeIdentity(): Identity | null {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    return null;
  }
  return {
    windowId: vscode.env.sessionId,
    // Canonical match key: percent-encoded, authority lowercased. NOT uri.authority
    // (case is unstable across windows) and NOT uri.fsPath (Windows-mangles remote).
    folderUri: folder.uri.toString(),
    remoteKind: remoteKind(),
    displayName: folder.name,
    pid: process.pid, // extension-host pid on the host the companion runs on (Windows)
  };
}

async function removeOwnFile(): Promise<void> {
  const target = lastWritten ?? stateFilePath();
  try {
    await fs.promises.unlink(target);
  } catch {
    // already gone (clean-exit delete is a latency optimization, not correctness)
  }
  lastWritten = undefined;
  lastSignature = undefined;
}

// mkdir is done once per shared dir, not on every heartbeat.
async function ensureDir(dir: string): Promise<void> {
  if (dirEnsured) {
    return;
  }
  await fs.promises.mkdir(dir, { recursive: true });
  dirEnsured = true;
}

// Full atomic write (temp + rename) so the dock never reads a torn file. Only on the
// first publish and whenever the reported identity changes.
async function writeFull(target: string, id: Identity): Promise<void> {
  await ensureDir(path.dirname(target));
  const payload: WindowState = {
    schemaVersion: SCHEMA_VERSION,
    ...id,
    lastSeen: new Date().toISOString(),
  };
  const tmp = `${target}.${process.pid}.tmp`;
  await fs.promises.writeFile(tmp, JSON.stringify(payload, null, 2), "utf8");
  await fs.promises.rename(tmp, target);
}

// Heartbeat. Identity unchanged -> just bump mtime (one syscall, no allocation, no
// temp-file churn). Identity changed (or first run / file vanished) -> full rewrite.
async function publish(): Promise<void> {
  const id = computeIdentity();
  if (!id) {
    await removeOwnFile();
    return;
  }
  const target = stateFilePath();
  const sig = JSON.stringify(id);
  try {
    if (sig !== lastSignature) {
      await writeFull(target, id);
      lastSignature = sig;
      if (lastWritten !== target) {
        log.appendLine(`reporting ${id.folderUri} -> ${target}`);
        lastWritten = target;
      }
    } else {
      const now = new Date();
      try {
        await fs.promises.utimes(target, now, now);
      } catch {
        await writeFull(target, id); // file disappeared -> recreate it
        lastWritten = target;
      }
    }
  } catch (err) {
    log.appendLine(`publish failed: ${String(err)}`);
  }
}

// (Re)schedule the heartbeat at the configured cadence, writing once immediately.
function restartHeartbeat(): void {
  if (heartbeat) {
    clearInterval(heartbeat);
  }
  void publish();
  heartbeat = setInterval(() => {
    void publish();
  }, heartbeatMs());
}

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  log = vscode.window.createOutputChannel("VS Code Projects Dock (companion)");
  context.subscriptions.push(log);

  restartHeartbeat();
  context.subscriptions.push({
    dispose: () => {
      if (heartbeat) {
        clearInterval(heartbeat);
      }
    },
  });

  // Folder opened/closed/changed within this same window: re-publish immediately.
  context.subscriptions.push(
    vscode.workspace.onDidChangeWorkspaceFolders(() => {
      void publish();
    })
  );

  // Settings changes take effect live: a new directory orphans the old file (delete
  // it first), and either change reschedules + rewrites at the new cadence/location.
  context.subscriptions.push(
    vscode.workspace.onDidChangeConfiguration(async (e) => {
      if (e.affectsConfiguration(`${CONFIG_SECTION}.sharedDirectory`)) {
        await removeOwnFile();
        dirEnsured = false; // the new directory may not exist yet
      }
      if (
        e.affectsConfiguration(`${CONFIG_SECTION}.heartbeatIntervalMs`) ||
        e.affectsConfiguration(`${CONFIG_SECTION}.sharedDirectory`)
      ) {
        restartHeartbeat();
      }
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("vscodeProjectsDock.showStatus", () => {
      log.show(true);
      const id = computeIdentity();
      log.appendLine("--- status ---");
      log.appendLine(`heartbeat: ${heartbeatMs()} ms`);
      log.appendLine(`sharedDir: ${sharedDir()}`);
      log.appendLine(`file:      ${stateFilePath()}`);
      log.appendLine(
        id
          ? JSON.stringify({ schemaVersion: SCHEMA_VERSION, ...id }, null, 2)
          : "(no folder open)"
      );
    })
  );
}

export async function deactivate(): Promise<void> {
  if (heartbeat) {
    clearInterval(heartbeat);
  }
  // Clean-exit delete: pure latency win. Crashes are caught by mtime staleness reap.
  await removeOwnFile();
}
