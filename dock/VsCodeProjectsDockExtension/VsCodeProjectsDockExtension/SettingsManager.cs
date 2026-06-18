// User-configurable settings, surfaced as the extension's settings page. Backed by
// JsonSettingsManager so the host persists/reloads them to FilePath; without that the
// values never round-trip and reads fall back to defaults.

using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

internal sealed class SettingsManager : JsonSettingsManager
{
    private const int DefaultTimeoutSeconds = 6;
    private const int MinTimeoutSeconds = 3;

    public SettingsManager()
    {
        FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VsCodeProjectsDock", "dock-settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        Settings.Add(new TextSetting(
            "sharedDirectory",
            "Shared state directory",
            "Folder the companion writes window state to. Must match the companion's "
                + "setting; clear it to reset to the default shown. Env vars like "
                + "%LOCALAPPDATA% are expanded.",
            WindowStore.DefaultSharedDir()));

        Settings.Add(new TextSetting(
            "windowTimeoutSeconds",
            "Window timeout (seconds)",
            "How long without a heartbeat before a window is treated as closed and removed "
                + "from the dock. Keep it a small multiple of the companion's heartbeat "
                + "(default 2s). Minimum 3.",
            "6"));

        var edition = new ChoiceSetSetting(
            "vsCodeEdition",
            "VS Code edition",
            "Which VS Code to launch and focus.",
            [
                new ChoiceSetSetting.Choice("Stable", "stable"),
                new ChoiceSetSetting.Choice("Insiders", "insiders"),
            ])
        {
            Value = "stable",
        };
        Settings.Add(edition);

        Settings.Add(new TextSetting(
            "vsCodePath",
            "VS Code executable path (override)",
            "Full path to the VS Code executable. Leave empty to auto-detect from the edition "
                + "above; set this for portable or non-standard installs.",
            string.Empty));

        // Load persisted values, then persist on every change.
        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    // The intended state directory (configured if set, else default) — for display.
    public string SharedDirectory
    {
        get
        {
            var value = Settings.GetSetting<string>("sharedDirectory")?.Trim();
            // Expand %LOCALAPPDATA% etc. — users paste env-var paths (the default hint
            // even shows one), and Directory.Exists won't match an unexpanded token.
            return string.IsNullOrEmpty(value)
                ? WindowStore.DefaultSharedDir()
                : Environment.ExpandEnvironmentVariables(value);
        }
    }

    // The directory to read: the effective path (configured if set, else default) when
    // it exists, else null — a missing folder is surfaced as a warning in the dock
    // rather than silently falling back, so a wrong configured path is visible.
    public string? ExistingSharedDirectory()
    {
        var dir = SharedDirectory;
        return Directory.Exists(dir) ? dir : null;
    }

    public int WindowTimeoutSeconds =>
        int.TryParse(Settings.GetSetting<string>("windowTimeoutSeconds"), out var v)
            ? Math.Max(MinTimeoutSeconds, v)
            : DefaultTimeoutSeconds;

    // Full path to the executable to launch (override wins, else edition auto-detect).
    public string VsCodeExecutable
    {
        get
        {
            var overridePath = Settings.GetSetting<string>("vsCodePath")?.Trim();
            return string.IsNullOrEmpty(overridePath)
                ? ResolveByEdition(IsInsiders)
                : Environment.ExpandEnvironmentVariables(overridePath);
        }
    }

    // Process name (no extension) of the windows to focus — derived from the executable
    // so it tracks the edition / override (Stable: "Code", Insiders: "Code - Insiders").
    public string VsCodeProcessName => Path.GetFileNameWithoutExtension(VsCodeExecutable);

    private bool IsInsiders => string.Equals(
        Settings.GetSetting<string>("vsCodeEdition"), "insiders", StringComparison.OrdinalIgnoreCase);

    private static string ResolveByEdition(bool insiders)
    {
        var folder = insiders ? "Microsoft VS Code Insiders" : "Microsoft VS Code";
        var exe = insiders ? "Code - Insiders.exe" : "Code.exe";
        string[] roots =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        ];

        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root))
            {
                continue;
            }
            var candidate = Path.Combine(root, folder, exe);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return exe; // last resort: rely on PATH
    }
}
