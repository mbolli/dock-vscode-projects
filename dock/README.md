# dock/ — PowerToys Command Palette dock extension

C# / WinUI 3 / Windows App SDK **packaged (MSIX)** app. Windows-only (no dotnet in
WSL; CmdPal is Windows-only). Reads the shared state directory, renders the band, owns
favourites, and performs focus/launch actions per `../contract/`.

## Scaffolding (corrected: there is NO `dotnet new` template)

Per Microsoft docs, generate the project from Command Palette itself:

1. Open Command Palette → run **"Create a new extension"**.
2. Fill the form: ExtensionName (valid C# class name, e.g. `VsCodeProjectsDock`), a
   human display name, and an Output Path.
3. Open the generated `.sln` in Visual Studio and **Deploy** (Build alone does not
   register the package). Then run `Reload` in Command Palette.

Prereqs: Visual Studio with the **WinUI / Windows App SDK** workloads, Windows 11 with
PowerToys, Developer mode enabled.

The generated tree carries its own `nuget.config`, `Directory.Packages.props` (pins
the `Microsoft.CommandPalette.Extensions` SDK version — currently 0.9.x on nuget.org),
an MSIX `Package.appxmanifest`, `Program.cs`, and `<Name>CommandsProvider.cs` (this is
where the band logic goes).

> Open question (was "build over \\wsl.localhost"): deploying a packaged MSIX whose
> project lives on `\\wsl.localhost\...` may fail — VS deploy / MSIX registration
> generally want a local NTFS path. Point the Output Path at the WSL `dock/` first and
> try Deploy; if it fails, we revisit where the dock project physically lives.
> Also un-ignore `**/Properties/launchSettings.json` and `*.pubxml` if the scaffold
> adds a full C# `.gitignore` — Windows App SDK needs them to deploy.
> Verify the dock-band API (`GetDockBands` / `WrappedDockItem`) exists in the pinned
> SDK version before building against it.
