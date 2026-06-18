import * as os from "os";
import * as path from "path";
import * as vscode from "vscode";

const EXTENSION_ID = "local.vscode-dock-phase0-probe";

// WSL view of %LOCALAPPDATA%\VsCodeProjectsDock\windows\ for the test-file write.
// If this extension runs on the Windows UI host, prefer the env var; on the Linux
// workspace host, fall back to the /mnt/c mount.
function sharedDir(): string {
  const localAppData = process.env.LOCALAPPDATA;
  if (localAppData) {
    return path.join(localAppData, "VsCodeProjectsDock", "windows");
  }
  return "/mnt/c/Users/micha/AppData/Local/VsCodeProjectsDock/windows";
}

function dump(out: vscode.OutputChannel): void {
  const self = vscode.extensions.getExtension(EXTENSION_ID);
  const folders = vscode.workspace.workspaceFolders ?? [];

  const lines: string[] = [];
  lines.push("================ Dock Phase 0 Probe ================");
  lines.push(`time:               ${new Date().toISOString()}`);
  lines.push("");
  lines.push("--- where am I running? (answers Q1 placement) ---");
  // extensionKind: 1 = UI host, 2 = Workspace host.
  lines.push(`extension.extensionKind: ${self?.extensionKind} (1=UI, 2=Workspace)`);
  lines.push(`process.platform:        ${process.platform}`);
  lines.push(`process.pid:             ${process.pid}`);
  lines.push(`os.hostname():           ${os.hostname()}`);
  lines.push(`LOCALAPPDATA present:    ${process.env.LOCALAPPDATA ? "yes (Windows-side)" : "no (Linux-side)"}`);
  lines.push("");
  lines.push("--- vscode.env (windowId candidates, answers Q2) ---");
  lines.push(`env.appHost:        ${vscode.env.appHost}`);
  lines.push(`env.remoteName:     ${vscode.env.remoteName ?? "(none -> local)"}`);
  lines.push(`env.machineId:      ${vscode.env.machineId}        <- per-machine, NOT per-window`);
  lines.push(`env.sessionId:      ${vscode.env.sessionId}        <- windowId candidate`);
  lines.push(`env.uriScheme:      ${vscode.env.uriScheme}`);
  lines.push("");
  lines.push("--- workspaceFolders (answers Q1: is wsl+ authority visible?) ---");
  if (folders.length === 0) {
    lines.push("(no folder open -- open a WSL folder and run 'Dock Probe: Dump window identity again')");
  }
  for (const f of folders) {
    const u = f.uri;
    lines.push(`  name:      ${f.name}`);
    lines.push(`  uri:       ${u.toString()}`);
    lines.push(`  scheme:    ${u.scheme}`);
    lines.push(`  authority: ${u.authority || "(empty)"}   <- expect 'wsl+Ubuntu' for a WSL folder`);
    lines.push(`  fsPath:    ${u.fsPath}`);
    lines.push("");
  }
  lines.push("====================================================");

  const text = lines.join("\n");
  out.appendLine(text);
  // Also to the debug console / dev tools, so it is visible however the probe is run.
  console.log(text);
}

export function activate(context: vscode.ExtensionContext): void {
  const out = vscode.window.createOutputChannel("Dock Phase 0 Probe");
  context.subscriptions.push(out);
  out.show(true);

  dump(out);

  context.subscriptions.push(
    vscode.commands.registerCommand("dockProbe.dump", () => {
      out.show(true);
      dump(out);
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("dockProbe.writeTestFile", async () => {
      const dir = sharedDir();
      const id = vscode.env.sessionId;
      const file = path.join(dir, `probe-${id}.json`);
      try {
        await vscode.workspace.fs.createDirectory(vscode.Uri.file(dir));
        const payload = Buffer.from(
          JSON.stringify(
            {
              wroteFrom:
                process.platform === "win32" ? "windows-ui-host" : "linux-workspace-host",
              sessionId: id,
              folderUri: vscode.workspace.workspaceFolders?.[0]?.uri.toString() ?? null,
              at: new Date().toISOString(),
            },
            null,
            2
          ),
          "utf8"
        );
        await vscode.workspace.fs.writeFile(vscode.Uri.file(file), payload);
        out.appendLine(`Wrote test file: ${file}`);
        vscode.window.showInformationMessage(`Dock probe wrote ${file}`);
      } catch (err) {
        out.appendLine(`FAILED to write ${file}: ${String(err)}`);
        vscode.window.showErrorMessage(`Dock probe write failed: ${String(err)}`);
      }
    })
  );
}

export function deactivate(): void {
  // nothing
}
