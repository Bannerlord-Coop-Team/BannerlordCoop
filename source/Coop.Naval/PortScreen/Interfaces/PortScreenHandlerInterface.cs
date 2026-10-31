using Coop.Naval.PortScreen.Data;
using GameInterface.Services;
using GameInterface.Services.Players.Interfaces;
using NavalDLC.CampaignBehaviors;
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
        // GiveGoldAction caps a payment at the payer's gold, so a trade the hero can't afford would hand ships over for less
        var shipCostModel = Campaign.Current.Models.ShipCostModel;
        int totalCost = 0;
        foreach (Ship ship in changes.ShipsToBuy)
        {
            if (!changes.LeftOwner.Ships.Contains(ship))
            {
                playerMessageInterface.SendInformationMessageToHero(changes.MainHero, $"The {ship.Name} was not bought because it is no longer available at {changes.MainHero.CurrentSettlement}");
                continue;
            }

            totalCost += (int)shipCostModel.GetShipTradeValue(ship, changes.LeftOwner, changes.RightOwner);
        }
        foreach (Ship ship in changes.ShipsToSell)
        {
            if (!changes.RightOwner.Ships.Contains(ship))
            {
                playerMessageInterface.SendInformationMessageToHero(changes.MainHero, $"The {ship.Name} was not sold because it is no longer available in {changes.MainHero}'s party.");
                continue;
            }

            totalCost -= (int)shipCostModel.GetShipTradeValue(ship, changes.RightOwner, changes.LeftOwner);
        }
        foreach (Ship ship in changes.ShipsToRepair)
        {
            if (ship.Owner == changes.RightOwner) totalCost += (int)shipCostModel.GetShipRepairCost(ship, changes.RightOwner);
        }
        foreach (ShipUpgradePieceInfo shipUpgradePieceInfo in changes.SelectedShipPieces)
        {
            if (shipUpgradePieceInfo.Ship.Owner == changes.RightOwner && shipUpgradePieceInfo.Piece != null)
            {
                totalCost += GetUpgradeCostOfShip(shipUpgradePieceInfo.Ship, shipUpgradePieceInfo.Piece, true, changes.RightOwner, changes.LeftOwner);
            }
        }
        if (totalCost > changes.MainHero.Gold)
        {
            playerMessageInterface.SendInformationMessageToHero(changes.MainHero, "The trade was cancelled because you do not have enough gold.");
            return;
        }

        // Sell before buying so the sale gold is there to pay for the bought ships, unlike vanilla
        foreach (Ship ship in changes.ShipsToSell)
        {
            if (!changes.RightOwner.Ships.Contains(ship)) continue;

            ChangeShipOwnerAction.ApplyByTrade(changes.LeftOwner, ship);
        }
        foreach (Ship ship in changes.ShipsToBuy)
        {
            if (!changes.LeftOwner.Ships.Contains(ship)) continue;

            ChangeShipOwnerAction.ApplyByTrade(changes.RightOwner, ship);
        }
        foreach (Ship ship in changes.ShipsToRepair)
        {
            if (ship.Owner != changes.RightOwner) continue;

            RepairShipAction.Apply(ship, changes.MainHero.CurrentSettlement);
        }
        RenameShips(changes, changes.RightOwner);
        foreach (ShipUpgradePieceInfo shipUpgradePieceInfo in changes.SelectedShipPieces)
        {
            if (shipUpgradePieceInfo.Ship.Owner != changes.RightOwner) continue;

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
            if (shipFigureheadInfo.Ship.Owner != changes.RightOwner) continue;

            shipFigureheadInfo.Ship.ChangeFigurehead(shipFigureheadInfo.Figurehead);
        }
        SendShipsToClan(changes, changes.RightOwner);
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

    private static void RenameShips(PortScreenChanges changes, PartyBase owner = null)
    {
        foreach (ShipRenameInfo shipRenameInfo in changes.ShipsToRename)
        {
            // Renaming ships without an owner is legitimate
            if (owner != null && shipRenameInfo.Ship.Owner != owner) continue;

            shipRenameInfo.Ship.SetName(new TextObject("{=!}" + shipRenameInfo.NewName, null));
        }
    }

    private static void SendShipsToClan(PortScreenChanges changes, PartyBase owner = null)
    {
        IFleetManagementCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IFleetManagementCampaignBehavior>();
        foreach (Ship shipToSend in changes.ShipsToSend)
        {
            // Renaming ships without an owner is legitimate
            if (owner != null && shipToSend.Owner != owner) continue;

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