# Changelog

## 0.1.0

Initial release.

- Reports each VS Code window's identity (folder URI, remote kind, display name,
  window/process id) and liveness to the shared state directory for the VS Code
  Projects Dock (PowerToys Command Palette) extension.
- Works for local and WSL remote windows from a single UI-host runtime.
- Cheap mtime-touch heartbeat; full rewrite only on folder change.
- Configurable heartbeat interval and shared directory (both apply live).
