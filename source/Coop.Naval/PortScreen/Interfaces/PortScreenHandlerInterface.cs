using Common.Logging;
using Coop.Naval.PortScreen.Data;
using Coop.Naval.PortScreen.Handlers;
using GameInterface.Services;
using GameInterface.Services.Players.Interfaces;
using NavalDLC.CampaignBehaviors;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using static NavalDLC.ViewModelCollection.Port.PortScreenHandlers.PortScreenHandler;

namespace Coop.Naval.PortScreen.Interfaces;

internal interface IPortScreenHandlerInterface : IGameAbstraction
{
    void OnConfirmChanges(PortScreenMode mode, PortScreenChanges changes);
}

internal class PortScreenHandlerInterface : IPortScreenHandlerInterface
{
    private readonly IPlayerMessageInterface playerMessageInterface;

    public PortScreenHandlerInterface(IPlayerMessageInterface playerMessageInterface)
    {
        this.playerMessageInterface = playerMessageInterface;
    }

    public void OnConfirmChanges(PortScreenMode mode, PortScreenChanges changes)
    {
        switch (mode)
        {
            case PortScreenMode.Trade: ConfirmTrade(changes); break;
            case PortScreenMode.Loot: ConfirmLoot(changes); break;
            case PortScreenMode.ManageFleet: ConfirmManageFleet(changes); break;
            case PortScreenMode.ManageOtherFleet: ConfirmManageOtherFleet(changes); break;
        }
    }

    private void ConfirmTrade(PortScreenChanges changes)
    {
        foreach (Ship ship in changes.ShipsToBuy)
        {
            if (!changes.LeftOwner.Ships.Contains(ship))
            {
                playerMessageInterface.SendInformationMessageToHero(changes.MainHero, $"The {ship.Name} was not bought because it is no longer available at {changes.MainHero.CurrentSettlement}");
                continue;
            }

            ChangeShipOwnerAction.ApplyByTrade(changes.RightOwner, ship);
        }
        foreach (Ship ship in changes.ShipsToSell)
        {
            if (!changes.RightOwner.Ships.Contains(ship))
            {
                playerMessageInterface.SendInformationMessageToHero(changes.MainHero, $"The {ship.Name} was not sold because it is no longer available in {changes.MainHero}'s party.");
                continue;
            }

            ChangeShipOwnerAction.ApplyByTrade(changes.LeftOwner, ship);
        }
        foreach (Ship ship in changes.ShipsToRepair)
        {
            RepairShipAction.Apply(ship, changes.MainHero.CurrentSettlement);
        }
        RenameShips(changes);
        foreach (ShipUpgradePieceInfo shipUpgradePieceInfo in changes.SelectedShipPieces)
        {
            int cost = 0;
            if (shipUpgradePieceInfo.Piece != null)
            {
                cost += GetUpgradeCostOfShip(shipUpgradePieceInfo.Ship, shipUpgradePieceInfo.Piece, true, changes.RightOwner, changes.LeftOwner);
            }
            shipUpgradePieceInfo.Ship.EquipUpgradePiece(shipUpgradePieceInfo.ShipSlotTag, shipUpgradePieceInfo.Piece);
            if (cost > 0)
            {
                GiveGoldAction.ApplyForCharacterToSettlement(changes.MainHero, changes.LeftOwner.Settlement, cost, false);
            }
            else
            {
                GiveGoldAction.ApplyForSettlementToCharacter(changes.LeftOwner.Settlement, changes.MainHero, -cost, false);
            }
        }
        foreach (ShipFigureheadInfo shipFigureheadInfo in changes.SelectedFigureheads)
        {
            shipFigureheadInfo.Ship.ChangeFigurehead(shipFigureheadInfo.Figurehead);
        }
        SendShipsToClan(changes);
        ResetAnchorIfNoShips(changes.MainParty);
        if (changes.MainParty.Ships.Count > 0 && !changes.MainParty.Anchor.IsValid && changes.LeftOwner.IsSettlement)
        {
            changes.MainParty.Anchor.SetSettlement(changes.LeftOwner.Settlement);
        }
    }
    
    private void ConfirmLoot(PortScreenChanges changes)
    {
        foreach (Ship ship in changes.ShipsToBuy)
        {
            ChangeShipOwnerAction.ApplyByLooting(changes.MainParty.Party, ship);
        }
        foreach (Ship ship in changes.ShipsToSell)
        {
            DestroyShipAction.ApplyByDiscard(ship);
        }
        SendShipsToClan(changes);
        RenameShips(changes);
        ResetAnchorIfNoShips(changes.MainParty);
    }

    private void ConfirmManageFleet(PortScreenChanges changes)
    {
        foreach (Ship ship in changes.ShipsToSell)
        {
            DestroyShipAction.ApplyByDiscard(ship);
        }
        foreach (Ship ship in changes.ShipsToBuy)
        {
            ChangeShipOwnerAction.ApplyByTransferring(changes.RightOwner, ship);
        }
        RenameShips(changes);
        SendShipsToClan(changes);
        ResetAnchorIfNoShips(changes.MainParty);
    }

    private void ConfirmManageOtherFleet(PortScreenChanges changes)
    {
        foreach (Ship ship in changes.ShipsToBuy)
        {
            ChangeShipOwnerAction.ApplyByTransferring(changes.MainParty.Party, ship);
        }
        foreach (Ship ship in changes.ShipsToSell)
        {
            ChangeShipOwnerAction.ApplyByTransferring(changes.RightOwner, ship);
        }
        ResetAnchorIfNoShips(changes.MainParty);
        if (changes.RightOwner.IsMobile)
        {
            ResetAnchorIfNoShips(changes.RightOwner.MobileParty);
        }
        RenameShips(changes);
    }

    private static void RenameShips(PortScreenChanges changes)
    {
        foreach (ShipRenameInfo shipRenameInfo in changes.ShipsToRename)
        {
            shipRenameInfo.Ship.SetName(new TextObject("{=!}" + shipRenameInfo.NewName, null));
        }
    }

    private static void SendShipsToClan(PortScreenChanges changes)
    {
        IFleetManagementCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IFleetManagementCampaignBehavior>();
        foreach (Ship shipToSend in changes.ShipsToSend)
        {
            campaignBehavior.SendShipToClan(shipToSend, changes.PlayerClan);
        }
    }

    private static void ResetAnchorIfNoShips(MobileParty party)
    {
        if (party.Ships.Count == 0 && party.Anchor.IsValid)
        {
            party.Anchor.ResetPosition();
        }
    }

    private int GetUpgradeCostOfShip(Ship ship, ShipUpgradePiece piece, bool isRightSideUpgrading, PartyBase rightOwner, PartyBase leftOwner)
    {
        PartyBase owner = isRightSideUpgrading ? rightOwner : leftOwner;
        return Campaign.Current.Models.ShipCostModel.GetShipUpgradePieceCost(ship, piece, owner);
    }
}