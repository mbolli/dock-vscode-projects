// Pin/unpin a project to the dock. Both keep the surface open and let the page's 2s
// poll re-render — no explicit refresh needed.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

internal sealed partial class PinCommand : InvokableCommand
{
    private readonly PinnedItem _item;

    public PinCommand(string id, PinnedItem item)
    {
        Id = id;
        Name = "Pin to dock";
        Icon = new IconInfo(char.ConvertFromUtf32(0xE718)); // Pin
        _item = item;
    }

    public override ICommandResult Invoke()
    {
        PinnedStore.Add(_item);
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class UnpinCommand : InvokableCommand
{
    private readonly string _matchKey;

    public UnpinCommand(string id, string matchKey)
    {
        Id = id;
        Name = "Unpin from dock";
        Icon = new IconInfo(char.ConvertFromUtf32(0xE77A)); // Unpin
        _matchKey = matchKey;
    }

    public override ICommandResult Invoke()
    {
        PinnedStore.Remove(_matchKey);
        return CommandResult.KeepOpen();
    }
}
