using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Coop.Core.Client.Services.BattleRetreat.Messages;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Retreat;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Leaving a battle MISSION with nothing decided — the scoreboard's retreat (issue #1840).
/// </summary>
/// <remarks>
/// Withdrawing the leaver's agents only empties the field; the party stayed on its side of the map event, so
/// the battle could never resolve and both players stayed registered in it. In the reported case — a PvP siege
/// whose attacker retreats while the defender is still deploying — the defender could not even notice: vanilla's
/// <c>BattleEndLogic</c> skips every end-condition check while a <c>BattleDeploymentHandler</c> is on the
/// mission, so nothing local could ever conclude it. The way out has to be server-driven, which is what these
/// cover: the party actually leaves the battle, and when that empties a side the battle is finalized and every
/// remaining player is told to close.
///
/// BattleState is deliberately never set. A victory state is what forfeits the losing side's roster and captures
/// its heroes; charging that to someone who walked out of an assault would take their whole army prisoner.
/// </remarks>
public class BattleMissionRetreatTests : MapEventTestBase
{
    public BattleMissionRetreatTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void LeavingABattleMissionUnresolved_IsForwardedToTheServer()
    {
        var ctx = CreateServerMapEvent();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(ctx.AttackerPartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));

            // What CoopBattleController.OnLeaving publishes when the mission produced no resolved result.
            client.Resolve<IMessageBroker>().Publish(this, new BattleMissionRetreatAttempted(party, mapEvent));
        }, MapEventDisabledMethods);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestBattleMissionRetreat>());
        Assert.Equal(ctx.AttackerPartyId, request.PartyId);
        Assert.Equal(ctx.MapEventId, request.MapEventId);
    }

    /// <summary>
    /// The reported case: a two-player battle whose attacker walks out of the mission. Its side is left with
    /// no parties, so the whole battle is finalized and every player is told to close its encounter — the
    /// defender does not have to notice anything itself.
    /// </summary>
    [Fact]
    public void MissionRetreatEmptyingASide_FinalizesTheBattle_AndClosesEveryPlayersEncounter()
    {
        var setup = SetupTwoOpposingPlayers();

        string[] closeOnDefender = null;
        Clients.Last().Resolve<IMessageBroker>()
            .Subscribe<NetworkClosePvpEncounter>(p => closeOnDefender = p.What.PartyIds);

        RequestMissionRetreat(Clients.First(), setup.Ctx.AttackerPartyId, setup.Ctx.MapEventId);

        // The battle is gone everywhere, so neither player is registered in a map event any more.
        AssertMapEventRemoved(Server, setup.Ctx.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, setup.Ctx.MapEventId);

        AssertNotInAnyBattle(Server, setup.Ctx.AttackerPartyId);
        AssertNotInAnyBattle(Server, setup.Ctx.DefenderPartyId);

        // The defender — the player who never left, and in the live game is still sitting in deployment — is
        // told to close, which is the update it never used to get.
        Assert.NotNull(closeOnDefender);
        Assert.Contains(setup.DefenderPartyBaseId, closeOnDefender);
    }

    /// <summary>
    /// One player retreating must not end the battle for the others: only that party leaves, and everyone
    /// else keeps fighting.
    /// </summary>
    [Fact]
    public void MissionRetreatWithAnAllyStillFighting_RemovesOnlyTheRetreatingParty()
    {
        var setup = SetupTwoOpposingPlayers();
        var allyPartyId = JoinNewServerPartyToSide(setup.Ctx.MapEventId, BattleSideEnum.Attacker);

        RequestMissionRetreat(Clients.First(), setup.Ctx.AttackerPartyId, setup.Ctx.MapEventId);

        // The battle survives with the ally and the defender still in it.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(setup.Ctx.MapEventId, out var mapEvent));
            Assert.False(mapEvent.IsFinalized);
            Assert.Equal(BattleState.None, mapEvent.BattleState);
        }, MapEventDisabledMethods);

        AssertNotInAnyBattle(Server, setup.Ctx.AttackerPartyId);
        AssertInBattle(Server, allyPartyId, setup.Ctx.MapEventId);
        AssertInBattle(Server, setup.Ctx.DefenderPartyId, setup.Ctx.MapEventId);

        // And the removal replicates: every other client drops it from its own copy of the battle.
        foreach (var client in Clients)
            AssertNotInAnyBattle(client, setup.Ctx.AttackerPartyId);
    }

    /// <summary>
    /// A mission that ended in a VICTORY leaves through the same code path, but its battle is owned by the
    /// completion barrier — the path that forfeits rosters and captures the losers. A late leave message must
    /// not divert it, or the losing side would be pulled out of the battle it just lost.
    /// </summary>
    [Fact]
    public void MissionRetreatOfAnAlreadyResolvedBattle_IsRefused()
    {
        var setup = SetupTwoOpposingPlayers();
        ResolveBattleOnServer(setup.Ctx.MapEventId, BattleState.AttackerVictory);

        RequestMissionRetreat(Clients.Last(), setup.Ctx.DefenderPartyId, setup.Ctx.MapEventId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(setup.Ctx.MapEventId, out var mapEvent));
            Assert.Equal(BattleState.AttackerVictory, mapEvent.BattleState);
        }, MapEventDisabledMethods);

        AssertInBattle(Server, setup.Ctx.DefenderPartyId, setup.Ctx.MapEventId);
    }

    /// <summary>
    /// The request carries two ids and nothing else, so the server re-derives who is allowed to leave: a peer
    /// may only pull out the party it controls. Otherwise one player could end another player's battle.
    /// </summary>
    [Fact]
    public void MissionRetreatForAPartyThePeerDoesNotOwn_IsRefused()
    {
        var setup = SetupTwoOpposingPlayers();

        // The attacker's peer names the DEFENDER's party.
        RequestMissionRetreat(Clients.First(), setup.Ctx.DefenderPartyId, setup.Ctx.MapEventId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(setup.Ctx.MapEventId, out var mapEvent));
            Assert.False(mapEvent.IsFinalized);
        }, MapEventDisabledMethods);

        AssertInBattle(Server, setup.Ctx.AttackerPartyId, setup.Ctx.MapEventId);
        AssertInBattle(Server, setup.Ctx.DefenderPartyId, setup.Ctx.MapEventId);
    }

    /// <summary>Sends the request the way the retreating client's handler does, over that client's peer.</summary>
    private void RequestMissionRetreat(EnvironmentInstance client, string partyId, string mapEventId)
    {
        var disabled = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(TaleWorlds.CampaignSystem.GameMenus.GameMenu), nameof(TaleWorlds.CampaignSystem.GameMenus.GameMenu.ExitToLast)))
            .ToList();

        Server.Call(
            () => Server.SimulateMessage(client.NetPeer, new NetworkRequestBattleMissionRetreat(partyId, mapEventId)),
            disabled);
    }

    private void AssertInBattle(EnvironmentInstance instance, string partyId, string mapEventId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.NotNull(party.Party.MapEventSide);
            Assert.True(instance.ObjectManager.TryGetId(party.Party.MapEventSide.MapEvent, out var actualId));
            Assert.Equal(mapEventId, actualId);
        });
    }

    private void AssertNotInAnyBattle(EnvironmentInstance instance, string partyId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Null(party.Party.MapEventSide);
        });
    }

    private void AssertMapEventRemoved(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _),
                $"MapEvent {mapEventId} should have been finalized and removed"));
    }

    /// <summary>Puts the battle in a decided state without running the loot/capture machinery headlessly.</summary>
    private void ResolveBattleOnServer(string mapEventId, BattleState battleState)
    {
        var disabled = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            mapEvent.BattleState = battleState;
            Assert.Equal(battleState, mapEvent.BattleState);
        }, disabled);
    }

    /// <summary>
    /// Two opposing player parties in one battle, each client owning its own as MainParty — the shape the
    /// issue describes.
    /// </summary>
    private OpposingPlayers SetupTwoOpposingPlayers()
    {
        var ctx = CreateServerMapEvent();

        RegisterAsPlayerParty("1", TestEnvironment.CreateRegisteredObject<Hero>(), ctx.AttackerPartyId);
        RegisterAsPlayerParty("2", TestEnvironment.CreateRegisteredObject<Hero>(), ctx.DefenderPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("1", Clients.First().NetPeer);
        Server.Resolve<IPlayerManager>().SetPeer("2", Clients.Last().NetPeer);

        SetMainParty(Clients.First(), ctx.AttackerPartyId);
        SetMainParty(Clients.Last(), ctx.DefenderPartyId);
        EnableHeadlessEncounterFinish(Clients.First());
        EnableHeadlessEncounterFinish(Clients.Last());

        return new OpposingPlayers(ctx, GetPartyBaseId(ctx.DefenderPartyId));
    }

    private void SetMainParty(EnvironmentInstance client, string partyId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
            Assert.NotNull(MobileParty.MainParty.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    private string GetPartyBaseId(string mobilePartyId)
    {
        string partyBaseId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyBaseId));
        });

        Assert.NotNull(partyBaseId);
        return partyBaseId;
    }

    private readonly record struct OpposingPlayers(MapEventContext Ctx, string DefenderPartyBaseId);
}
