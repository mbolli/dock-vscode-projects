// Opens (or focuses, since VS Code reuses a window already showing the folder) the
// project behind a dock button. Launch flag confirmed in Phase 0 (Q4): the stored
// folderUri is passed verbatim as `--folder-uri`, no transformation.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

internal sealed partial class LaunchVsCodeCommand : InvokableCommand
{
    private readonly string _folderUri;
    private readonly string? _focusDisplayName;
    private readonly string? _focusRemoteKind;

    // Closed target (e.g. a favourite whose window isn't open): launch only.
    public LaunchVsCodeCommand(string id, string folderUri)
        : this(id, folderUri, null, null)
    {
    }

    // Open target: try a fast native window focus first; fall back to launch if the
    // window can't be uniquely identified.
    public LaunchVsCodeCommand(string id, string folderUri, string? focusDisplayName, string? focusRemoteKind)
    {
        Id = id;
        Name = "Open";
        Icon = new IconInfo(char.ConvertFromUtf32(0xE8A7)); // open in new window
        _folderUri = folderUri;
        _focusDisplayName = focusDisplayName;
        _focusRemoteKind = focusRemoteKind;
    }

    public override ICommandResult Invoke()
    {
        // Fast path: raise the already-open window directly. Only when the project is
        // known open (focus hint present) and the window is unambiguously matched.
        if (_focusDisplayName is not null &&
            WindowFocus.TryFocus(_focusDisplayName, _focusRemoteKind ?? "local"))
        {
            return CommandResult.Dismiss();
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ResolveCodeExe(),
                UseShellExecute = false,
            };
            // ArgumentList quotes for us — the folderUri carries ':' '/' '%' which must
            // reach VS Code unmangled to match the open window.
            psi.ArgumentList.Add("--folder-uri");
            psi.ArgumentList.Add(_folderUri);
            Process.Start(psi);
        }
        catch (Exception)
        {
            // Code.exe missing or refused to start: nothing useful to do from a dock
            // button. Don't take the dock process down over a failed launch.
        }

        return CommandResult.Dismiss();
    }

    // Prefer the known install paths so this works even when the MSIX container's PATH
    // doesn't carry VS Code's bin dir; fall back to PATH resolution as a last resort.
    private static string ResolveCodeExe()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code", "Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft VS Code", "Code.exe"),
        ];

        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                return c;
            }
        }

        return "Code.exe";
    }
}
