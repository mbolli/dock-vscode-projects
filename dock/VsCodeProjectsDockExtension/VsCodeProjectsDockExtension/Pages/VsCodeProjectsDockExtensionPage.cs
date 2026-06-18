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
    private const string IdPrefix = "us.bolli.vscodeprojectsdock";
    private const int PollMs = 2000;

    private readonly System.Timers.Timer _poll;

    public VsCodeProjectsDockExtensionPage()
    {
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
        var live = WindowStore.ReadLive();
        var favourites = FavouritesStore.Read();
        var favouriteKeys = new HashSet<string>(favourites.Select(f => f.MatchKey));

        var items = new List<IListItem>();

        foreach (var f in favourites)
        {
            var open = live.Any(w => w.FolderUri == f.MatchKey);
            items.Add(new ListItem(new LaunchVsCodeCommand(IdPrefix + ".fav." + f.MatchKey, f.LaunchTarget))
            {
                Title = f.Label,
                Subtitle = open ? "favourite · open" : "favourite",
                Icon = new IconInfo(""), // FavoriteStarFill
                MoreCommands =
                [
                    new CommandContextItem(
                        new UnpinFavouriteCommand(IdPrefix + ".unpin." + f.MatchKey, f.MatchKey)),
                ],
            });
        }

        foreach (var w in live.Where(w => !favouriteKeys.Contains(w.FolderUri)))
        {
            items.Add(new ListItem(new LaunchVsCodeCommand(IdPrefix + ".window." + w.WindowId, w.FolderUri))
            {
                Title = w.DisplayName,
                Subtitle = w.RemoteKind,
                Icon = new IconInfo(""), // "{ }" placeholder glyph
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
            // Keep one item so the band still pins when nothing is open or favourited.
            items.Add(new ListItem(new NoOpCommand() { Id = IdPrefix + ".empty" })
            {
                Title = "No VS Code windows",
                Icon = new IconInfo(""),
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
