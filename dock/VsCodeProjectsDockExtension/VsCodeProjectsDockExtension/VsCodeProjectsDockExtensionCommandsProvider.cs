// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

public partial class VsCodeProjectsDockExtensionCommandsProvider : CommandProvider
{
    // Reverse-DNS provider id — required for the dock to address this extension.
    private const string ProviderId = "us.bolli.vscodeprojectsdock";
    private const string BandId = ProviderId + ".band";

    private readonly ICommandItem[] _commands;

    public VsCodeProjectsDockExtensionCommandsProvider()
    {
        Id = ProviderId;
        DisplayName = "VS Code Projects Dock";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands = [
            new CommandItem(new VsCodeProjectsDockExtensionPage()) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    // Group 4: render one button per live window read from the shared state dir.
    // Read fresh each call. Actions (focus/launch) are Group 6; staleness reaping is
    // Group 5; live refresh on a timer is Group 8. For now this is read-only display.
    public override ICommandItem[]? GetDockBands()
    {
        var windows = ReadWindows();

        IListItem[] buttons = windows.Count == 0
            ? [new ListItem(new NoOpCommand() { Id = ProviderId + ".empty" })
                {
                    Title = "No VS Code windows",
                    Icon = new IconInfo(""), // "info" glyph
                }]
            : windows.Select(w => (IListItem)new ListItem(
                    new NoOpCommand() { Id = ProviderId + ".window." + w.WindowId })
                {
                    Title = w.DisplayName,
                    Subtitle = w.RemoteKind, // hint until per-kind icons differentiate
                    Icon = new IconInfo(""), // Segoe "Code" glyph
                }).ToArray();

        return [new WrappedDockItem(buttons, BandId, "VS Code Projects")];
    }

    // The companion's default location. Must match the companion's sharedDirectory
    // setting — dock-side settings parity is a later task; the default is assumed here.
    private static string SharedDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VsCodeProjectsDock", "windows");

    private readonly record struct WindowState(
        string WindowId, string FolderUri, string RemoteKind, string DisplayName);

    private static List<WindowState> ReadWindows()
    {
        var dir = SharedDir();
        var windows = new List<WindowState>();
        if (!Directory.Exists(dir))
        {
            return windows;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
                var root = doc.RootElement;
                var windowId = GetString(root, "windowId");
                var folderUri = GetString(root, "folderUri");
                var displayName = GetString(root, "displayName");
                // folderUri is the match key; windowId is the dedup/Id key. Skip any
                // file missing them (a torn write or a foreign file in the dir).
                if (string.IsNullOrEmpty(windowId) || string.IsNullOrEmpty(folderUri))
                {
                    continue;
                }
                windows.Add(new WindowState(
                    windowId,
                    folderUri,
                    GetString(root, "remoteKind") ?? "local",
                    string.IsNullOrEmpty(displayName) ? folderUri : displayName));
            }
            catch (JsonException)
            {
                // Mid-write torn file (the companion writes atomically, but a foreign
                // or truncated file shouldn't take the whole band down).
            }
            catch (IOException)
            {
                // Locked/disappeared between enumerate and read — skip this tick.
            }
        }

        return windows;
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
