using Coop.Naval.PortScreen.Data;
using Coop.Naval.PortScreen.Interfaces;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Naval.PortScreen;

/// <summary>
/// Prices what the server will actually charge for a port trade or repair, so it can refuse one the player can no longer afford.
/// </summary>
internal interface IPortTransactionPricer
{
    /// <summary>Net gold the trade costs the right owner, after sale proceeds, skipping ships no longer available.</summary>
    int GetTradeCost(PortScreenChanges changes);

    /// <summary>Gold the owner pays to repair every damaged ship it owns.</summary>
    int GetRepairCost(PartyBase owner);
}

/// <summary>
/// Prices port transactions in the order the server applies them: sales, then purchases, then repairs and upgrades.
/// </summary>
internal class PortTransactionPricer : IPortTransactionPricer
{
    private readonly IShipCostInterface shipCostInterface;

    public PortTransactionPricer(IShipCostInterface shipCostInterface)
    {
        this.shipCostInterface = shipCostInterface;
    }

    public int GetTradeCost(PortScreenChanges changes)
    {
        var sold = changes.ShipsToSell.Where(ship => changes.RightOwner.Ships.Contains(ship)).ToList();
        var bought = changes.ShipsToBuy.Where(ship => changes.LeftOwner.Ships.Contains(ship)).ToList();

        int cost = bought.Sum(ship => shipCostInterface.GetTradeValue(ship, changes.LeftOwner, changes.RightOwner));
        cost -= sold.Sum(ship => shipCostInterface.GetTradeValue(ship, changes.RightOwner, changes.LeftOwner));
        cost += changes.ShipsToRepair
            .Where(ship => IsOwnedAfterTrade(ship, changes.RightOwner, bought, sold))
            .Sum(ship => shipCostInterface.GetRepairCost(ship, changes.RightOwner));
        cost += changes.SelectedShipPieces
            .Where(info => info.Piece != null && IsOwnedAfterTrade(info.Ship, changes.RightOwner, bought, sold))
            .Sum(info => shipCostInterface.GetUpgradeCost(info.Ship, info.Piece, changes.RightOwner));

        return cost;
    }

    public int GetRepairCost(PartyBase owner) => owner.Ships
        .Where(ship => ship.HitPoints < ship.MaxHitPoints)
        .Sum(ship => shipCostInterface.GetRepairCost(ship, owner));

    // The server only repairs and upgrades ships the right owner holds once the sales and purchases are done
    private static bool IsOwnedAfterTrade(Ship ship, PartyBase rightOwner, List<Ship> bought, List<Ship> sold) =>
        bought.Contains(ship) || (ship.Owner == rightOwner && !sold.Contains(ship));
}
