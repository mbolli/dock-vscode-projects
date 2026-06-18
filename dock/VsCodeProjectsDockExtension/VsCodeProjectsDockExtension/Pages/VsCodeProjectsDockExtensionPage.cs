// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

// The live window list. Used for both the top-level palette command and the dock band
// (each GetItems() entry renders as a band button). A timer re-reads the shared dir and
// raises ItemsChanged so the band reflects windows opening/closing without a manual
// reload. Group 8 (Q5): whether the dock actually re-queries on this signal for a dock
// band is the thing this verifies.
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

    public override IListItem[] GetItems()
    {
        var windows = WindowStore.ReadLive();
        if (windows.Count == 0)
        {
            // Keep one item so the band still pins when nothing is open.
            return [new ListItem(new NoOpCommand() { Id = IdPrefix + ".empty" })
            {
                Title = "No VS Code windows",
                Icon = new IconInfo(""),
            }];
        }

        return windows
            .Select(w => (IListItem)new ListItem(
                new LaunchVsCodeCommand(IdPrefix + ".window." + w.WindowId, w.FolderUri))
            {
                Title = w.DisplayName,
                Subtitle = w.RemoteKind,
                Icon = new IconInfo(""), // Segoe "Code" glyph
            })
            .ToArray();
    }

    public void Dispose()
    {
        _poll.Stop();
        _poll.Dispose();
    }
}
