; Inno Setup script for the VS Code Projects Dock Command Palette extension.
; Unpackaged (winget) distribution: installs the published files and registers the
; out-of-proc COM server so Command Palette can activate the extension. The CLSID must
; match [Guid("...")] on the VsCodeProjectsDockExtension class.
; build-exe.ps1 rewrites AppVersion, the source path, and the architecture per build.

#define AppVersion "0.1.0"

[Setup]
AppId={{6966910f-85db-4e1a-8e1a-b1fc92e2fe5c}
AppName=VS Code Projects Dock
AppVersion={#AppVersion}
AppPublisher=zwei und eins gmbh
AppPublisherURL=https://github.com/mbolli/dock-vscode-projects
DefaultDirName={autopf}\VsCodeProjectsDock
OutputDir=bin\Release\installer
OutputBaseFilename=VsCodeProjectsDock-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0.19041

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bin\Release\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\VS Code Projects Dock"; Filename: "{app}\VsCodeProjectsDockExtension.exe"

[Registry]
; Register the extension's COM server (out-of-proc) for Command Palette discovery.
Root: HKCU; Subkey: "SOFTWARE\Classes\CLSID\{{6966910f-85db-4e1a-8e1a-b1fc92e2fe5c}"; ValueType: string; ValueName: ""; ValueData: "VsCodeProjectsDockExtension"; Flags: uninsdeletekey
Root: HKCU; Subkey: "SOFTWARE\Classes\CLSID\{{6966910f-85db-4e1a-8e1a-b1fc92e2fe5c}\LocalServer32"; ValueType: string; ValueName: ""; ValueData: "{app}\VsCodeProjectsDockExtension.exe -RegisterProcessAsComServer"; Flags: uninsdeletekey
