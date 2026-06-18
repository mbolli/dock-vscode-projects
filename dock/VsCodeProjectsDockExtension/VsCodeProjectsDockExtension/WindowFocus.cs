// Native focus for an already-open VS Code window — faster than relaunching Code.exe.
// Probe finding: every VS Code window is owned by the one main Code.exe process, so the
// state file's pid (the extension host) can't pick a window; the window TITLE does. We
// match on the folder name plus the [WSL] tag remote windows carry. Ambiguous (0 or >1
// matches) → return false so the caller falls back to a --folder-uri launch.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VsCodeProjectsDockExtension;

internal static class WindowFocus
{
    private const string VsCodeTitleMarker = "Visual Studio Code";
    private const int SW_RESTORE = 9;

    internal static bool TryFocus(string displayName, string remoteKind, string processName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        var matches = new List<IntPtr>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
            {
                return true;
            }
            var title = GetWindowTitle(hwnd);
            if (title.Length == 0 || !title.Contains(VsCodeTitleMarker, StringComparison.Ordinal))
            {
                return true;
            }
            if (IsProcess(hwnd, processName) && TitleMatches(title, displayName, remoteKind))
            {
                matches.Add(hwnd);
            }
            return true;
        }, IntPtr.Zero);

        // Exactly one match is safe to raise; anything else is ambiguous → let the
        // caller launch by URI instead ("one-window-off is self-correcting").
        return matches.Count == 1 && Focus(matches[0]);
    }

    private static bool TitleMatches(string title, string displayName, string remoteKind)
    {
        if (!title.Contains(displayName, StringComparison.Ordinal))
        {
            return false;
        }
        // The same folder opened remotely vs locally must not cross-match: WSL windows
        // tag the title "[WSL: <distro>]"; local windows don't.
        var wslTitle = title.Contains("[WSL", StringComparison.OrdinalIgnoreCase);
        return remoteKind == "wsl" ? wslTitle : !wslTitle;
    }

    private static bool IsProcess(IntPtr hwnd, string processName)
    {
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // process exited between enum and lookup
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool Focus(IntPtr hwnd)
    {
        // Attach our thread to the current foreground thread so Windows grants the
        // focus change instead of downgrading it to a taskbar flash (foreground lock).
        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var thisThread = GetCurrentThreadId();
        var attached = foregroundThread != thisThread &&
                       AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            ShowWindow(hwnd, SW_RESTORE);
            BringWindowToTop(hwnd);
            return SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(thisThread, foregroundThread, false);
            }
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var len = GetWindowTextLength(hwnd);
        if (len <= 0)
        {
            return string.Empty;
        }
        var buffer = new char[len + 1];
        var written = GetWindowText(hwnd, buffer, buffer.Length);
        return written > 0 ? new string(buffer, 0, written) : string.Empty;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(IntPtr hwnd, [Out] char[] text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
}
