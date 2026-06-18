// Opens (or focuses, since VS Code reuses a window already showing the folder) the
// project behind a dock button. Launch flag confirmed in Phase 0 (Q4): the stored
// folderUri is passed verbatim as `--folder-uri`, no transformation.

using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

// Identifies an open window to fast-focus. Absent for closed targets (launch only).
internal readonly record struct FocusHint(string DisplayName, string RemoteKind, string ProcessName);

internal sealed partial class LaunchVsCodeCommand : InvokableCommand
{
    private readonly string _folderUri;
    private readonly string _executable;
    private readonly FocusHint? _focus;

    public LaunchVsCodeCommand(string id, string folderUri, string executable, FocusHint? focus = null)
    {
        Id = id;
        Name = "Open";
        Icon = new IconInfo(char.ConvertFromUtf32(0xE8A7)); // open in new window
        _folderUri = folderUri;
        _executable = executable;
        _focus = focus;
    }

    public override ICommandResult Invoke()
    {
        // Fast path: raise the already-open window directly. Only when the project is
        // known open (focus hint present) and the window is unambiguously matched.
        if (_focus is { } focus &&
            WindowFocus.TryFocus(focus.DisplayName, focus.RemoteKind, focus.ProcessName))
        {
            return CommandResult.Dismiss();
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _executable,
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
}
