// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

// The live window list + pinned projects. Used for both the top-level palette command and
// the dock band (each GetItems() entry renders as a band button). A timer re-reads and
// raises ItemsChanged so the band reflects windows opening/closing and pin/unpin without
// a manual reload.
internal sealed partial class VsCodeProjectsDockExtensionPage : ListPage, IDisposable
{
    private const string IdPrefix = "ch.mbolli.vscodeprojectsdock";
    private const int PollMs = 2000;

    private readonly System.Timers.Timer _poll;
    private readonly SettingsManager _settings;

    public VsCodeProjectsDockExtensionPage(SettingsManager settings)
    {
        _settings = settings;

        // Stable id so CmdPal keys the dock-band pin to it and dedups across re-installs
        // instead of generating a fresh id each time (which accumulated duplicate pins).
        Id = IdPrefix + ".windows";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "VS Code Projects";
        Name = "VS Code Projects";

        _poll = new System.Timers.Timer(PollMs) { AutoReset = true };
        _poll.Elapsed += (_, _) => RaiseItemsChanged(0);
        _poll.Start();
    }

    // Three-state merge: pinned projects on the left (merged once with a live window if that
    // project is open), then live-only windows on the right. Dedup is keyed on folderUri.
    public override IListItem[] GetItems()
    {
        var sharedDir = _settings.ExistingSharedDirectory();
        var live = sharedDir is null
            ? new List<WindowState>()
            : WindowStore.ReadLive(sharedDir, _settings.WindowTimeoutSeconds);
        var pinned = PinnedStore.Read();
        var pinnedKeys = new HashSet<string>(pinned.Select(p => p.MatchKey));

        var executable = _settings.VsCodeExecutable;
        var processName = _settings.VsCodeProcessName;
        var items = new List<IListItem>();

        // Surface a missing shared folder up front — even when projects are pinned —
        // since live-window detection is broken until it's fixed.
        if (sharedDir is null)
        {
            items.Add(new ListItem(new NoOpCommand() { Id = IdPrefix + ".missing" })
            {
                Title = "VS Code companion not found",
                Subtitle = "No shared folder at " + _settings.SharedDirectory,
                Icon = new IconInfo(char.ConvertFromUtf32(0xE7BA)), // Warning
            });
        }

        foreach (var p in pinned)
        {
            // If the pinned project is open, carry the live window's name/kind so the
            // command can fast-focus it; otherwise the command just launches.
            var match = live.FirstOrDefault(w => w.FolderUri == p.MatchKey);
            var open = match.FolderUri is not null;
            var command = open
                ? new LaunchVsCodeCommand(IdPrefix + ".pinned." + p.MatchKey, p.LaunchTarget, executable,
                    new FocusHint(match.DisplayName, match.RemoteKind, processName))
                : new LaunchVsCodeCommand(IdPrefix + ".pinned." + p.MatchKey, p.LaunchTarget, executable);
            items.Add(new ListItem(command)
            {
                Title = p.Label,
                Subtitle = open ? "pinned · open" : "pinned",
                // Filled star when the project is open, outline when it's closed.
                Icon = new IconInfo(char.ConvertFromUtf32(open ? 0xE735 : 0xE734)),
                MoreCommands =
                [
                    new CommandContextItem(
                        new UnpinCommand(IdPrefix + ".unpin." + p.MatchKey, p.MatchKey)),
                ],
            });
        }

        foreach (var w in live.Where(w => !pinnedKeys.Contains(w.FolderUri)))
        {
            items.Add(new ListItem(new LaunchVsCodeCommand(IdPrefix + ".window." + w.WindowId, w.FolderUri, executable,
                new FocusHint(w.DisplayName, w.RemoteKind, processName)))
            {
                Title = w.DisplayName,
                Subtitle = w.RemoteKind,
                Icon = new IconInfo(char.ConvertFromUtf32(0xF12B)), // FabricFolder (solid)
                MoreCommands =
                [
                    new CommandContextItem(new PinCommand(
                        IdPrefix + ".pin." + w.WindowId,
                        new PinnedItem(w.DisplayName, w.FolderUri, w.FolderUri))),
                ],
            });
        }

        if (items.Count == 0)
        {
            // Folder exists but nothing is open or pinned. (A missing folder already
            // added a warning above, so we're not here in that case.)
            items.Add(new ListItem(new NoOpCommand() { Id = IdPrefix + ".empty" })
            {
                Title = "No VS Code windows",
                Icon = new IconInfo(char.ConvertFromUtf32(0xE946)),
            });
        }

        return items.ToArray();
    }

    public void Dispose()
    {
        _poll.Stop();
        _poll.Dispose();
    }
}
