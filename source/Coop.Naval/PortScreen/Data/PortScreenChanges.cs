using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using static NavalDLC.ViewModelCollection.Port.PortScreenHandlers.PortScreenHandler;

namespace Coop.Naval.PortScreen.Data;

internal readonly struct PortScreenChanges
{
    public readonly PartyBase RightOwner;
    public readonly PartyBase LeftOwner;
    public readonly Hero MainHero;
    public readonly Clan PlayerClan;
    public readonly MobileParty MainParty;
    public readonly MBReadOnlyList<Ship> ShipsToBuy;
    public readonly MBReadOnlyList<Ship> ShipsToSell;
    public readonly MBReadOnlyList<Ship> ShipsToRepair;
    public readonly MBReadOnlyList<ShipRenameInfo> ShipsToRename;
    public readonly MBReadOnlyList<ShipUpgradePieceInfo> SelectedShipPieces;
    public readonly MBReadOnlyList<ShipFigureheadInfo> SelectedFigureheads;
    public readonly MBReadOnlyList<Ship> ShipsToSend;

    public PortScreenChanges(
        PartyBase rightOwner,
        PartyBase leftOwner,
        Hero mainHero,
        Clan playerClan,
        MobileParty mainParty,
        MBReadOnlyList<Ship> shipsToBuy,
        MBReadOnlyList<Ship> shipsToSell,
        MBReadOnlyList<Ship> shipsToRepair,
        MBReadOnlyList<ShipRenameInfo> shipsToRename,
        MBReadOnlyList<ShipUpgradePieceInfo> selectedShipPieces,
        MBReadOnlyList<ShipFigureheadInfo> selectedFigureheads,
        MBReadOnlyList<Ship> shipsToSend)
    {
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
