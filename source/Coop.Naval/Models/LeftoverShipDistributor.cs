using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.Naval.Models;

/// <summary>[Server] Hands the naval leftover ships vanilla offers the main party to the winning players instead.</summary>
public interface ILeftoverShipDistributor
{
    /// <summary>Gives each leftover ship to the player whose contribution share <paramref name="rollContribution"/> lands in.</summary>
    void GiveToPlayers(Dictionary<Ship, MapEventParty> distributedShips, IEnumerable<Ship> leftoverShips, IReadOnlyList<MapEventParty> playerParties, Func<int> rollContribution);
}

/// <summary>Splits vanilla's main party roll over the winners' total contribution among the winning players, by their order in it.</summary>
public class LeftoverShipDistributor : ILeftoverShipDistributor
{
    // Vanilla's cap on the ships the main party's loot screen offers.
    private const int MaxLootShips = 25;

    // A roll on a player's share gives that player the ship, a roll on an AI winner's share ends the handout as vanilla does,
    // and a player at the cap skips the ship.
    public void GiveToPlayers(Dictionary<Ship, MapEventParty> distributedShips, IEnumerable<Ship> leftoverShips, IReadOnlyList<MapEventParty> playerParties, Func<int> rollContribution)
    {
        foreach (Ship ship in leftoverShips)
        {
            MapEventParty looter = PlayerAtContributionRoll(playerParties, rollContribution());
            if (looter == null) return;
            if (distributedShips.Count(x => x.Value == looter) >= MaxLootShips) continue;

            distributedShips[ship] = looter;
        }
    }

    // The player whose part of the winners' total contribution the roll falls in, or null for an AI winner's part.
    private static MapEventParty PlayerAtContributionRoll(IReadOnlyList<MapEventParty> playerParties, int roll)
    {
        foreach (MapEventParty playerParty in playerParties)
        {
            if (roll < playerParty.ContributionToBattle) return playerParty;

            roll -= playerParty.ContributionToBattle;
        }

        return null;
    }
}
