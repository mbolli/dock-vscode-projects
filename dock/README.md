# dock/ — PowerToys Command Palette dock extension

C# / WinUI 3 / Windows App SDK **packaged (MSIX)** app. Windows-only (no dotnet in
WSL; CmdPal is Windows-only). Reads the shared state directory, renders the band, owns
pinned projects, and performs focus/launch actions per `../contract/`.

## Scaffolding

There's no `dotnet new` template — generate the project from Command Palette:

1. Open Command Palette → run **"Create a new extension"**.
2. Fill the form: ExtensionName (valid C# class name, e.g. `VsCodeProjectsDock`), a
   human display name, and an Output Path.

The generated tree carries its own `nuget.config`, `Directory.Packages.props` (pins
the `Microsoft.CommandPalette.Extensions` SDK version — currently `0.9.260303001`),
an MSIX `Package.appxmanifest`, `Program.cs`, and `<Name>CommandsProvider.cs` (this is
where the band logic goes).

## Build & deploy (dev loop)

Build over the `\\wsl.localhost` UNC path, but register from a local NTFS copy — don't
use VS's Deploy. Key points:

- **Build with the standalone `dotnet` CLI**, not VS's bundled MSBuild (VS 2022 lacks the
  .NET 10 SDK; you'll hit `MSB4236 / Microsoft.NET.Sdk not found`). `dotnet` restores and
  builds fine from `\\wsl.localhost\...`.
- **You cannot register the MSIX layout in place from `\\wsl.localhost`.** WSL is a **9P**
  filesystem; `Add-AppxPackage -Register` over it fails with `0x80073CFD`
  ("cannot deploy in path of filesystem type 9P"). UNC *reads* are fine — only the
  *install location* must be NTFS. So we copy the built layout to a local `C:\` path and
  register from there. **The dock project stays in the monorepo on WSL** — only the
  deploy target moves.
- **Developer Mode must be ON** (`HKLM\...\AppModelUnlock\AllowDevelopmentWithoutDevLicense=1`,
  Settings → System → For developers), or register fails `0x80073CFF` (no dev license).
- The dock-band API (`GetDockBands` returning `ICommandItem[]?`, `WrappedDockItem`) is in
  the pinned SDK 0.9.x and supported by the CmdPal runtime (PowerToys 0.100+ /
  `Microsoft.CommandPalette` 0.11.x).

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

## Registering / re-registering

**Prereq:** Developer Mode ON (see above).

**Re-register only** (layout in `C:\dev-deploy` already current — e.g. CmdPal dropped it,
or you just want a clean re-register without rebuilding):

```powershell
$dst = 'C:\dev-deploy\VsCodeProjectsDock'
Get-Process VsCodeProjectsDockExtension -ErrorAction SilentlyContinue | Stop-Process -Force
Get-AppxPackage VsCodeProjectsDockExtension | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Register (Join-Path $dst 'AppxManifest.xml')
Get-AppxPackage VsCodeProjectsDockExtension | Select-Object Version, InstallLocation
```

**Unregister** (remove entirely):

```powershell
Get-AppxPackage VsCodeProjectsDockExtension | Remove-AppxPackage
```

### Getting CmdPal to show the update

`Reload` in Command Palette refreshes the command list but **not** cached provider
metadata — icon, the settings page, capabilities. After changing any of those, **fully
quit and relaunch Command Palette** (stop `Microsoft.CmdPal.UI`), don't just Reload.

CmdPal caches discovered providers under
`%LOCALAPPDATA%\Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\` in
`commandProviderCache.json` and `settings.json`. Changing the package **Publisher**
changes its family hash (`<Name>_<hash>!App!ID`), so prior-identity builds linger there
as **ghost entries** (an old duplicate that won't have your latest settings/icon). With
CmdPal stopped, remove the stale `<Name>_<oldhash>!App!ID` entries from both files
(back them up first), then relaunch. Likewise, duplicate **dock bands** are pinned in
`settings.json` under `CenterBands` — dedupe to one entry keyed on the band's CommandId.

## Release — Microsoft Store

Distribution is the **Microsoft Store**, which signs the MSIX for you (no certificate to
manage). This is required, not just convenient: CmdPal discovers extensions through the
**package catalog** (the `com.microsoft.commandpalette` AppExtension), so an extension
must be a **signed, identity-bearing package**. An unpackaged EXE with only a
`CLSID`/`LocalServer32` registration is *not* discoverable, so the winget "unpackaged"
route doesn't apply here.

1. Enroll in the [Microsoft Store developer program](https://developer.microsoft.com/microsoft-store/register)
   (free as of 2026; a GmbH goes through company verification). Then **Apps and Games →
   New product → MSIX app**, reserve the name, and copy the three **Product Identity**
   values.
2. Set the identity from Partner Center:
   - `Package.appxmanifest` → `Identity Name`, `Identity Publisher`, `PublisherDisplayName`
   - `.csproj` → `AppxPackageIdentityName`, `AppxPackagePublisher`, `AppxPackageVersion`

   (Changing the identity changes the package-family hash, so the local dev build
   re-registers under the new identity and pins reset — expected.)
3. Build the **unsigned** bundle (the Store signs at submission):

   ```powershell
   .\build-store.ps1 -Version 0.1.0.0
   # -> AppPackages\VsCodeProjectsDockExtension_<version>.msixbundle (x64 + ARM64)
   ```

4. In Partner Center, **start a submission**, upload the `.msixbundle`, fill the listing,
   and add certification notes (the extension requires PowerToys with Command Palette).
   Certification takes ~1–3 business days; the Store auto-updates installed users on each
   new submission.

> Note: Store-published extensions load in CmdPal once installed, but don't appear in
> CmdPal's *browse/Search WinGet* experience. For that, additionally list on winget
> (e.g. a manifest referencing the Store package) — separate from this MSIX submission.
