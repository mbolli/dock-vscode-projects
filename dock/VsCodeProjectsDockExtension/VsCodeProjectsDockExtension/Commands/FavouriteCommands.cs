// Pin/unpin a project to the dock's favourites. Both keep the surface open and let the
// page's 2s poll re-render — no explicit refresh needed.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VsCodeProjectsDockExtension;

internal sealed partial class PinFavouriteCommand : InvokableCommand
{
    private readonly Favourite _fav;

    public PinFavouriteCommand(string id, Favourite fav)
    {
        Id = id;
        Name = "Pin to dock";
        Icon = new IconInfo(""); // Pin
        _fav = fav;
    }

    public override ICommandResult Invoke()
    {
        FavouritesStore.Add(_fav);
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class UnpinFavouriteCommand : InvokableCommand
{
    private readonly string _matchKey;

    public UnpinFavouriteCommand(string id, string matchKey)
    {
        Id = id;
        Name = "Unpin from dock";
        Icon = new IconInfo(""); // Unpin
        _matchKey = matchKey;
    }

    public override ICommandResult Invoke()
    {
        FavouritesStore.Remove(_matchKey);
        return CommandResult.KeepOpen();
    }
}
