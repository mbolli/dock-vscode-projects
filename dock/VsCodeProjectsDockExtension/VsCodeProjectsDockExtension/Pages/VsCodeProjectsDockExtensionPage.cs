// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

// The live window list + favourites. Used for both the top-level palette command and
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

    // Three-state merge: favourites pinned left (merged once with a live window if that
    // project is open), then live-only windows on the right. Dedup is keyed on folderUri.
    public override IListItem[] GetItems()
    {
        var sharedDir = _settings.ExistingSharedDirectory();
        var live = sharedDir is null
            ? new List<WindowState>()
            : WindowStore.ReadLive(sharedDir, _settings.WindowTimeoutSeconds);
        var favourites = FavouritesStore.Read();
        var favouriteKeys = new HashSet<string>(favourites.Select(f => f.MatchKey));

        var executable = _settings.VsCodeExecutable;
        var processName = _settings.VsCodeProcessName;
        var items = new List<IListItem>();

        foreach (var f in favourites)
        {
            // If the favourite's project is open, carry the live window's name/kind so
            // the command can fast-focus it; otherwise the command just launches.
            var match = live.FirstOrDefault(w => w.FolderUri == f.MatchKey);
            var open = match.FolderUri is not null;
            var command = open
                ? new LaunchVsCodeCommand(IdPrefix + ".fav." + f.MatchKey, f.LaunchTarget, executable,
                    new FocusHint(match.DisplayName, match.RemoteKind, processName))
                : new LaunchVsCodeCommand(IdPrefix + ".fav." + f.MatchKey, f.LaunchTarget, executable);
            items.Add(new ListItem(command)
            {
                Title = f.Label,
                Subtitle = open ? "favourite · open" : "favourite",
                Icon = new IconInfo(char.ConvertFromUtf32(0xE735)), // star (favourite)
                MoreCommands =
                [
                    new CommandContextItem(
                        new UnpinFavouriteCommand(IdPrefix + ".unpin." + f.MatchKey, f.MatchKey)),
                ],
            });
        }

        foreach (var w in live.Where(w => !favouriteKeys.Contains(w.FolderUri)))
        {
            items.Add(new ListItem(new LaunchVsCodeCommand(IdPrefix + ".window." + w.WindowId, w.FolderUri, executable,
                new FocusHint(w.DisplayName, w.RemoteKind, processName)))
            {
                Title = w.DisplayName,
                Subtitle = w.RemoteKind,
                Icon = new IconInfo(char.ConvertFromUtf32(0xE8B7)), // folder (live window)
                MoreCommands =
                [
                    new CommandContextItem(new PinFavouriteCommand(
                        IdPrefix + ".pin." + w.WindowId,
                        new Favourite(w.DisplayName, w.FolderUri, w.FolderUri))),
                ],
            });
        }

        if (items.Count == 0)
        {
            // Keep one item so the band still pins. Distinguish "the companion's folder
            // isn't there" (likely not installed / misconfigured) from "folder is there
            // but nothing is open" — the former is a warning, not an empty state.
            items.Add(sharedDir is null
                ? new ListItem(new NoOpCommand() { Id = IdPrefix + ".missing" })
                {
                    Title = "VS Code companion not found",
                    Subtitle = "No shared folder at " + _settings.SharedDirectory,
                    Icon = new IconInfo(char.ConvertFromUtf32(0xE7BA)), // Warning
                }
                : new ListItem(new NoOpCommand() { Id = IdPrefix + ".empty" })
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
