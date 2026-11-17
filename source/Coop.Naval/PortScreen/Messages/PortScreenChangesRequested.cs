using Common.Messaging;
using Coop.Naval.PortScreen.Data;
using NavalDLC.ViewModelCollection.Port.PortScreenHandlers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Coop.Naval.PortScreen.Messages;

public readonly struct PortScreenChangesRequested : IEvent
{
    public readonly PortScreenMode Mode;
    public readonly PartyBase RightOwner;
    public readonly PartyBase LeftOwner;
    public readonly Hero MainHero;
    public readonly Clan PlayerClan;
    public readonly MobileParty MainParty;
    public readonly MBReadOnlyList<Ship> ShipsToBuy;
    public readonly MBReadOnlyList<Ship> ShipsToSell;
    public readonly MBReadOnlyList<Ship> ShipsToRepair;
    public readonly MBReadOnlyList<PortScreenHandler.ShipRenameInfo> ShipsToRename;
    public readonly MBReadOnlyList<PortScreenHandler.ShipUpgradePieceInfo> SelectedShipPieces;
    public readonly MBReadOnlyList<PortScreenHandler.ShipFigureheadInfo> SelectedFigureheads;
    public readonly MBReadOnlyList<Ship> ShipsToSend;

    public PortScreenChangesRequested(
        PortScreenMode mode,
        PartyBase rightOwner,
        PartyBase leftOwner,
        Hero mainHero,
        Clan playerClan,
        MobileParty mainParty,
        MBReadOnlyList<Ship> shipsToBuy,
        MBReadOnlyList<Ship> shipsToSell,
        MBReadOnlyList<Ship> shipsToRepair,
        MBReadOnlyList<PortScreenHandler.ShipRenameInfo> shipsToRename,
        MBReadOnlyList<PortScreenHandler.ShipUpgradePieceInfo> selectedShipPieces,
        MBReadOnlyList<PortScreenHandler.ShipFigureheadInfo> selectedFigureheads,
        MBReadOnlyList<Ship> shipsToSend)
    {
        Mode = mode;
        RightOwner = rightOwner;
        LeftOwner = leftOwner;
        MainHero = mainHero;
        PlayerClan = playerClan;
        MainParty = mainParty;
        ShipsToBuy = shipsToBuy;
        ShipsToSell = shipsToSell;
        ShipsToRepair = shipsToRepair;
        ShipsToRename = shipsToRename;
        SelectedShipPieces = selectedShipPieces;
        SelectedFigureheads = selectedFigureheads;
        ShipsToSend = shipsToSend;
    }
}
