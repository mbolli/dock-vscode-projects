// User-configurable settings, surfaced as the extension's settings page and persisted
// by the CmdPal host. Values are read fresh each poll, so changes apply within a tick.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

internal sealed class SettingsManager
{
    private const int DefaultTimeoutSeconds = 6;
    private const int MinTimeoutSeconds = 3;

    private readonly Settings _settings = new();

    public SettingsManager()
    {
        _settings.Add(new TextSetting(
            "sharedDirectory",
            "Shared state directory",
            "Folder the companion writes window state to. Leave empty for the default "
                + "(%LOCALAPPDATA%\\VsCodeProjectsDock\\windows). Must match the companion's setting.",
            string.Empty));

        _settings.Add(new TextSetting(
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
        _settings.Add(edition);

        _settings.Add(new TextSetting(
            "vsCodePath",
            "VS Code executable path (override)",
            "Full path to the VS Code executable. Leave empty to auto-detect from the edition "
                + "above; set this for portable or non-standard installs.",
            string.Empty));
    }

    public Settings Settings => _settings;

    // The companion's default location, used when the setting is left empty.
    public string SharedDirectory
    {
        get
        {
            var value = _settings.GetSetting<string>("sharedDirectory")?.Trim();
            return string.IsNullOrEmpty(value) ? WindowStore.DefaultSharedDir() : value;
        }
    }

    public int WindowTimeoutSeconds =>
        int.TryParse(_settings.GetSetting<string>("windowTimeoutSeconds"), out var v)
            ? Math.Max(MinTimeoutSeconds, v)
            : DefaultTimeoutSeconds;

    // Full path to the executable to launch (override wins, else edition auto-detect).
    public string VsCodeExecutable
    {
        get
        {
            var overridePath = _settings.GetSetting<string>("vsCodePath")?.Trim();
            return string.IsNullOrEmpty(overridePath) ? ResolveByEdition(IsInsiders) : overridePath;
        }
    }

    // Process name (no extension) of the windows to focus — derived from the executable
    // so it tracks the edition / override (Stable: "Code", Insiders: "Code - Insiders").
    public string VsCodeProcessName => Path.GetFileNameWithoutExtension(VsCodeExecutable);

    private bool IsInsiders => string.Equals(
        _settings.GetSetting<string>("vsCodeEdition"), "insiders", StringComparison.OrdinalIgnoreCase);

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
