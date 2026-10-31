#if COOP_NAVAL
using Coop.Naval.PortScreen;
using Coop.Naval.PortScreen.Data;
using Coop.Naval.PortScreen.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Tests.Naval;

/// <summary>
/// Builds bare ships, parties and heroes with just the state the port transaction code reads.
/// </summary>
internal static class PortTestObjects
{
    /// <summary>Creates a party with an empty fleet.</summary>
    public static PartyBase CreateParty()
    {
        var party = (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));
        AccessTools.Field(typeof(PartyBase), "_ships").SetValue(party, new MBList<Ship>());
        return party;
    }

    /// <summary>Creates a ship in the owner's fleet with the given hit points out of 100.</summary>
    public static Ship CreateShip(PartyBase owner, float hitPoints = 100f)
    {
        var hull = (ShipHull)RuntimeHelpers.GetUninitializedObject(typeof(ShipHull));
        Traverse.Create(hull).Property(nameof(ShipHull.MaxHitPoints)).SetValue(100);
        var ship = (Ship)RuntimeHelpers.GetUninitializedObject(typeof(Ship));
        AccessTools.Field(typeof(Ship), nameof(Ship.ShipHull)).SetValue(ship, hull);
        AccessTools.Field(typeof(Ship), "_shipPieces").SetValue(ship, new Dictionary<string, ShipUpgradePiece>());
        AccessTools.Field(typeof(Ship), "_hitPoints").SetValue(ship, hitPoints);
        AccessTools.Field(typeof(Ship), "_owner").SetValue(ship, owner);
        ((MBList<Ship>)AccessTools.Field(typeof(PartyBase), "_ships").GetValue(owner)).Add(ship);
        return ship;
    }

    /// <summary>Creates a hero holding the given gold.</summary>
    public static Hero CreateHero(int gold)
    {
        var hero = (Hero)RuntimeHelpers.GetUninitializedObject(typeof(Hero));
        AccessTools.Field(typeof(Hero), "_gold").SetValue(hero, gold);
        return hero;
    }

    /// <summary>Creates an upgrade piece with no stats.</summary>
    public static ShipUpgradePiece CreatePiece() => (ShipUpgradePiece)RuntimeHelpers.GetUninitializedObject(typeof(ShipUpgradePiece));
}

/// <summary>
/// Fixed ship prices keyed by ship and buyer, standing in for the campaign's ship cost model.
/// </summary>
internal sealed class FakeShipCostInterface : IShipCostInterface
{
    public readonly Dictionary<(Ship, PartyBase), int> TradeValues = new();
    public readonly Dictionary<Ship, int> RepairCosts = new();
    public readonly Dictionary<(Ship, ShipUpgradePiece), int> UpgradeCosts = new();

    public int GetTradeValue(Ship ship, PartyBase seller, PartyBase buyer) => TradeValues.TryGetValue((ship, buyer), out var value) ? value : 0;

    public int GetRepairCost(Ship ship, PartyBase owner) => RepairCosts.TryGetValue(ship, out var value) ? value : 0;

    public int GetUpgradeCost(Ship ship, ShipUpgradePiece piece, PartyBase owner) => UpgradeCosts.TryGetValue((ship, piece), out var value) ? value : 0;
}

/// <summary>
/// A fixed repair price, standing in for the port transaction pricer.
/// </summary>
internal sealed class FakePortTransactionPricer : IPortTransactionPricer
{
    public int RepairCost;

    public int GetTradeCost(PortScreenChanges changes) => 0;

    public int GetRepairCost(PartyBase owner) => RepairCost;
}
#endif
