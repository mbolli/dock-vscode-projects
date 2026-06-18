import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";

// Contract: see ../contract/state-file.schema.json (single source of truth).
const SCHEMA_VERSION = 1;
const HEARTBEAT_MS = 2000;

type RemoteKind = "local" | "wsl" | "container" | "ssh";

interface WindowState {
  schemaVersion: number;
  windowId: string;
  folderUri: string;
  remoteKind: RemoteKind;
  displayName: string;
  lastSeen: string;
}

let log: vscode.OutputChannel;
let heartbeat: ReturnType<typeof setInterval> | undefined;
let lastWritten: string | undefined; // path of the file we currently own, if any

// %LOCALAPPDATA%\VsCodeProjectsDock\windows\ — present because Phase 0 fixed this to
// the ui (Windows) host. The /mnt/c fallback only matters if placement ever changes.
function sharedDir(): string {
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
function computeState(): WindowState | null {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    return null;
  }
  return {
    schemaVersion: SCHEMA_VERSION,
    windowId: vscode.env.sessionId,
    // Canonical match key: percent-encoded, authority lowercased. NOT uri.authority
    // (case is unstable across windows) and NOT uri.fsPath (Windows-mangles remote).
    folderUri: folder.uri.toString(),
    remoteKind: remoteKind(),
    displayName: folder.name,
    lastSeen: new Date().toISOString(),
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
}

// Rewrite-on-heartbeat. Atomic (temp + rename) so the dock never reads a torn file.
async function publish(): Promise<void> {
  const state = computeState();
  if (!state) {
    await removeOwnFile();
    return;
  }
  const target = stateFilePath();
  const tmp = `${target}.${process.pid}.tmp`;
  try {
    await fs.promises.mkdir(sharedDir(), { recursive: true });
    await fs.promises.writeFile(tmp, JSON.stringify(state, null, 2), "utf8");
    await fs.promises.rename(tmp, target);
    if (lastWritten !== target) {
      log.appendLine(`reporting ${state.folderUri} -> ${target}`);
      lastWritten = target;
    }
  } catch (err) {
    log.appendLine(`publish failed: ${String(err)}`);
  }
}

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  log = vscode.window.createOutputChannel("VS Code Projects Dock (companion)");
  context.subscriptions.push(log);

  await publish();

  heartbeat = setInterval(() => {
    void publish();
  }, HEARTBEAT_MS);
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

  context.subscriptions.push(
    vscode.commands.registerCommand("vscodeProjectsDock.showStatus", () => {
      log.show(true);
      const state = computeState();
      log.appendLine("--- status ---");
      log.appendLine(`sharedDir: ${sharedDir()}`);
      log.appendLine(`file:      ${stateFilePath()}`);
      log.appendLine(state ? JSON.stringify(state, null, 2) : "(no folder open)");
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
