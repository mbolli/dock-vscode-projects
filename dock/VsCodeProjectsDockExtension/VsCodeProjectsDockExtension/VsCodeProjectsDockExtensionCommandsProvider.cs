// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

public partial class VsCodeProjectsDockExtensionCommandsProvider : CommandProvider
{
    // Reverse-DNS provider id — required for the dock to address this extension.
    private const string ProviderId = "us.bolli.vscodeprojectsdock";

    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _dockBands;

    public VsCodeProjectsDockExtensionCommandsProvider()
    {
        Id = ProviderId;
        DisplayName = "VS Code Projects Dock";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands = [
            new CommandItem(new VsCodeProjectsDockExtensionPage()) { Title = DisplayName },
        ];

        // Group 2: one hardcoded button so we can confirm the band pins and renders.
        // The window-driven buttons (read from the shared state dir) come in Group 4.
        var button = new ListItem(new NoOpCommand() { Id = ProviderId + ".placeholder" })
        {
            Title = "VS Code Projects",
            Icon = new IconInfo(""), // Segoe "Code" glyph
        };
        _dockBands = [
            new WrappedDockItem([button], ProviderId + ".band", "VS Code Projects"),
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override ICommandItem[]? GetDockBands()
    {
        return _dockBands;
    }
}
