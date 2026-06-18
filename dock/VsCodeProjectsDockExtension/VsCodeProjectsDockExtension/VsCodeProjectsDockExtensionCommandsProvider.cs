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

    // One live page drives both surfaces: the top-level palette entry and the dock
    // band. Its timer raises ItemsChanged, so both refresh as windows come and go.
    private readonly VsCodeProjectsDockExtensionPage _windowsPage = new();
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _dockBands;

    public VsCodeProjectsDockExtensionCommandsProvider()
    {
        Id = ProviderId;
        DisplayName = "VS Code Projects Dock";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands = [new CommandItem(_windowsPage) { Title = DisplayName }];
        _dockBands = [new CommandItem(_windowsPage) { Title = "VS Code Projects" }];
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
