#if COOP_NAVAL
using Coop.Naval.Models;
using GameInterface.Services.Players;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.Naval;

/// <summary>
/// The naval leftover ships vanilla rolls for the main party go to whichever winning player the contribution roll lands on.
/// </summary>
public class LeftoverShipDistributorTests
{
    private readonly MapEventParty aiWinner = CreateMapEventParty(contribution: 10);
    private readonly MapEventParty firstPlayer = CreateMapEventParty(contribution: 10);
    private readonly MapEventParty secondPlayer = CreateMapEventParty(contribution: 10);
    private readonly LeftoverShipDistributor distributor;
    private readonly MapEventParty[] winners;

    public LeftoverShipDistributorTests()
    {
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(players => players.Contains(firstPlayer.Party.MobileParty)).Returns(true);
        playerManager.Setup(players => players.Contains(secondPlayer.Party.MobileParty)).Returns(true);
        distributor = new LeftoverShipDistributor(playerManager.Object);
        winners = new[] { aiWinner, firstPlayer, secondPlayer };
    }

    // #4860: only the top player could win a leftover ship. The players' shares are 0-9 and 10-19 with the AI winner's after
    // them, so 20 ends the handout and the last ship stays unlooted even though its roll would have hit the first player.
    [Fact]
    public void RollOnSecondPlayersShare_GivesItTheShip_AndAnAiRollEndsTheHandout()
    {
        var ships = CreateShips(3);
        var distributedShips = ships.ToDictionary(ship => ship, _ => (MapEventParty)null);

        distributor.GiveToPlayers(distributedShips, ships, winners, ScriptedRolls(10, 20, 0));

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

        distributor.GiveToPlayers(distributedShips, ships, winners, ScriptedRolls(0, 10));

        Assert.Equal(25, distributedShips.Count(x => x.Value == firstPlayer));
        Assert.Same(ships[1], Assert.Single(distributedShips, x => x.Value == secondPlayer).Key);
    }

    // Returns the rolls in order, failing if a roll isn't over the winners' 30 total contribution or comes past the script.
    private static Func<int, int> ScriptedRolls(params int[] rolls)
    {
        var queue = new Queue<int>(rolls);
        return max =>
        {
            Assert.Equal(30, max);
            return queue.Dequeue();
        };
    }

    // Creates bare ships, only ever compared by reference.
    private static List<Ship> CreateShips(int count) =>
        Enumerable.Range(0, count).Select(_ => (Ship)RuntimeHelpers.GetUninitializedObject(typeof(Ship))).ToList();

    // Creates a winner with its own mobile party and just its battle contribution.
    private static MapEventParty CreateMapEventParty(int contribution)
    {
        var party = (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));
        party.MobileParty = (MobileParty)RuntimeHelpers.GetUninitializedObject(typeof(MobileParty));
        var mapEventParty = (MapEventParty)RuntimeHelpers.GetUninitializedObject(typeof(MapEventParty));
        mapEventParty.Party = party;
        mapEventParty._contributionToBattle = contribution;
        return mapEventParty;
    }
}
#endif
