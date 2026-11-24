using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.Naval.Models;

/// <summary>[Server] Hands the naval leftover ships vanilla offers the main party to the winning players instead.</summary>
public interface ILeftoverShipDistributor
{
    /// <summary>
    /// Rolls each leftover ship with <paramref name="randomInt"/> over the winners' total contribution and gives it to the
    /// player whose share the roll lands in.
    /// </summary>
    void GiveToPlayers(Dictionary<Ship, MapEventParty> distributedShips, IEnumerable<Ship> leftoverShips, IReadOnlyList<MapEventParty> winnerParties, Func<int, int> randomInt);
}

/// <summary>
/// Lays the players' contribution shares out in list order with the AI winners' share after them, and gives each leftover
/// ship to the player whose share its roll lands in.
/// </summary>
public class LeftoverShipDistributor : ILeftoverShipDistributor
{
    // Vanilla's cap on the ships the main party's loot screen offers.
    private const int MaxLootShips = 25;

    private readonly IPlayerManager playerManager;

    public LeftoverShipDistributor(IPlayerManager playerManager)
    {
        this.playerManager = playerManager;
    }

    // A roll on an AI winner's share ends the handout as vanilla does, and a player at the cap skips the ship.
    public void GiveToPlayers(Dictionary<Ship, MapEventParty> distributedShips, IEnumerable<Ship> leftoverShips, IReadOnlyList<MapEventParty> winnerParties, Func<int, int> randomInt)
    {
        var playerParties = winnerParties.Where(x => playerManager.Contains(x.Party.MobileParty)).ToList();
        int totalContribution = winnerParties.Sum(x => x.ContributionToBattle);
        foreach (Ship ship in leftoverShips)
        {
            MapEventParty looter = PlayerAtContributionRoll(playerParties, randomInt(totalContribution));
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
