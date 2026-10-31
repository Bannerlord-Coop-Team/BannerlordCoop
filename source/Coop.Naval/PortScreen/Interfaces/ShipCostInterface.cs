using GameInterface.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Coop.Naval.PortScreen.Interfaces;

/// <summary>
/// Reads ship trade, repair and upgrade prices from the campaign's ship cost model.
/// </summary>
internal interface IShipCostInterface : IGameAbstraction
{
    /// <summary>Gold the buyer pays the seller for the ship, in whole gold like vanilla.</summary>
    int GetTradeValue(Ship ship, PartyBase seller, PartyBase buyer);

    /// <summary>Gold the owner pays to repair the ship, in whole gold like vanilla.</summary>
    int GetRepairCost(Ship ship, PartyBase owner);

    /// <summary>Gold the owner pays to fit the upgrade piece on the ship.</summary>
    int GetUpgradeCost(Ship ship, ShipUpgradePiece piece, PartyBase owner);
}

/// <summary>
/// Reads ship prices from <see cref="Campaign.Current"/>'s ship cost model.
/// </summary>
internal class ShipCostInterface : IShipCostInterface
{
    public int GetTradeValue(Ship ship, PartyBase seller, PartyBase buyer) =>
        (int)Campaign.Current.Models.ShipCostModel.GetShipTradeValue(ship, seller, buyer);

    public int GetRepairCost(Ship ship, PartyBase owner) =>
        (int)Campaign.Current.Models.ShipCostModel.GetShipRepairCost(ship, owner);

    public int GetUpgradeCost(Ship ship, ShipUpgradePiece piece, PartyBase owner) =>
        Campaign.Current.Models.ShipCostModel.GetShipUpgradePieceCost(ship, piece, owner);
}
