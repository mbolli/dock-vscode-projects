# dock/ — PowerToys Command Palette dock extension

C# / WinUI 3 / Windows App SDK **packaged (MSIX)** app. Windows-only (no dotnet in
WSL; CmdPal is Windows-only). Reads the shared state directory, renders the band, owns
favourites, and performs focus/launch actions per `../contract/`.

## Scaffolding (corrected: there is NO `dotnet new` template)

Per Microsoft docs, generate the project from Command Palette itself:

1. Open Command Palette → run **"Create a new extension"**.
2. Fill the form: ExtensionName (valid C# class name, e.g. `VsCodeProjectsDock`), a
   human display name, and an Output Path.

The generated tree carries its own `nuget.config`, `Directory.Packages.props` (pins
the `Microsoft.CommandPalette.Extensions` SDK version — currently `0.9.260303001`),
an MSIX `Package.appxmanifest`, `Program.cs`, and `<Name>CommandsProvider.cs` (this is
where the band logic goes).

## Build & deploy — the dev loop (RESOLVED)

The original "build over `\\wsl.localhost`" assumption was only half right, so don't
follow the VS-Deploy path. What was settled empirically (2026-06-18):

- **Build over the UNC path works** — but use the standalone `dotnet` CLI, **not** VS's
  bundled MSBuild (VS 2022 lacks the .NET 10 SDK; you'll hit `MSB4236 / Microsoft.NET.Sdk
  not found`). `dotnet` (10.0.301) restores and builds fine from `\\wsl.localhost\...`.
- **You cannot register the MSIX layout in place from `\\wsl.localhost`.** WSL is a **9P**
  filesystem; `Add-AppxPackage -Register` over it fails with `0x80073CFD`
  ("cannot deploy in path of filesystem type 9P"). UNC *reads* are fine — only the
  *install location* must be NTFS. So we copy the built layout to a local `C:\` path and
  register from there. **The dock project stays in the monorepo on WSL** — only the
  deploy target moves.
- **Developer Mode must be ON** (`HKLM\...\AppModelUnlock\AllowDevelopmentWithoutDevLicense=1`,
  Settings → System → For developers), or register fails `0x80073CFF` (no dev license).
- The dock-band API (`GetDockBands` returning `ICommandItem[]?`, `WrappedDockItem`) is
  **confirmed** present in the pinned SDK 0.9.x and supported by the installed CmdPal
  runtime (PowerToys 0.100.0 / `Microsoft.CommandPalette` 0.11.x).

Run from Windows (PowerShell), from this project dir over the UNC path:

```powershell
# 1. Build (loose MSIX layout lands in bin\x64\Debug\...\win-x64\, incl. AppxManifest.xml)
dotnet build VsCodeProjectsDockExtension\VsCodeProjectsDockExtension.csproj -p:Platform=x64 -c Debug

# 2. Copy the layout to a local NTFS path (9P can't host a registered package)
$src = '.\VsCodeProjectsDockExtension\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64'
$dst = 'C:\dev-deploy\VsCodeProjectsDock'
robocopy $src $dst /MIR   # exit codes 1-3 are success

# 3. Stop the running instance (CmdPal holds the COM-server EXE), drop the old
#    registration, then register the fresh layout.
Get-Process VsCodeProjectsDockExtension -ErrorAction SilentlyContinue | Stop-Process -Force
Get-AppxPackage VsCodeProjectsDockExtension | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Register (Join-Path $dst 'AppxManifest.xml')
```

Then `Reload` in Command Palette (or restart it) to pick up band changes.

> Why remove-then-register: re-registering the *same* version in place fails with
> `0x80073CFB` ("already installed, reinstall blocked") once the package exists. The
> `Remove-AppxPackage` makes every deploy idempotent without bumping the version each
> build. (And stop the EXE first, or you'll hit `0x80073D02` — files in use.)

> Note: the loose-layout `AppxManifest.xml` is only generated when a packaging build
> runs; the first time, build with
> `-p:UapAppxPackageBuildMode=SideloadOnly -p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=true`
> to emit it. Subsequent plain `dotnet build`s refresh the payload DLLs in that layout.
