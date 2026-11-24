#if COOP_NAVAL
using Coop.Naval.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using Xunit;

namespace Coop.Tests.Naval;

/// <summary>
/// The naval leftover ships vanilla rolls for the main party go to whichever winning player the contribution roll lands on.
/// </summary>
public class LeftoverShipDistributorTests
{
    private readonly LeftoverShipDistributor distributor = new LeftoverShipDistributor();
    private readonly MapEventParty firstPlayer = CreateMapEventParty(contribution: 10);
    private readonly MapEventParty secondPlayer = CreateMapEventParty(contribution: 10);

    // #4860: only the top player could win a leftover ship. A roll past the players' shares lands on an AI winner and ends the
    // handout, so the last ship stays unlooted even though its roll would have hit the first player.
    [Fact]
    public void RollOnSecondPlayersShare_GivesItTheShip_AndAnAiRollEndsTheHandout()
    {
        var ships = CreateShips(3);
        var distributedShips = ships.ToDictionary(ship => ship, _ => (MapEventParty)null);

        distributor.GiveToPlayers(distributedShips, ships, new[] { firstPlayer, secondPlayer }, ScriptedRolls(15, 25, 0));

        Assert.Same(ships[0], Assert.Single(distributedShips, x => x.Value == secondPlayer).Key);
        Assert.DoesNotContain(distributedShips, x => x.Value == firstPlayer);
    }

    // A player already holding vanilla's 25 loot ships skips its roll, and the next roll can still give another player a ship.
    [Fact]
    public void PlayerAtTheCap_SkipsTheShip_AndTheNextRollStillPays()
    {
        var distributedShips = CreateShips(25).ToDictionary(ship => ship, _ => firstPlayer);
        var ships = CreateShips(2);
        foreach (var ship in ships) distributedShips.Add(ship, null);

        distributor.GiveToPlayers(distributedShips, ships, new[] { firstPlayer, secondPlayer }, ScriptedRolls(0, 15));

        Assert.Equal(25, distributedShips.Count(x => x.Value == firstPlayer));
        Assert.Same(ships[1], Assert.Single(distributedShips, x => x.Value == secondPlayer).Key);
    }

    // Returns the rolls in order, throwing if the distributor rolls more often than scripted.
    private static Func<int> ScriptedRolls(params int[] rolls) => new Queue<int>(rolls).Dequeue;

    // Creates bare ships, only ever compared by reference.
    private static List<Ship> CreateShips(int count) =>
        Enumerable.Range(0, count).Select(_ => (Ship)RuntimeHelpers.GetUninitializedObject(typeof(Ship))).ToList();

    // Creates a winner with just its battle contribution.
    private static MapEventParty CreateMapEventParty(int contribution)
    {
        var mapEventParty = (MapEventParty)RuntimeHelpers.GetUninitializedObject(typeof(MapEventParty));
        mapEventParty._contributionToBattle = contribution;
        return mapEventParty;
    }
}
#endif
