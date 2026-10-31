using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using HarmonyLib;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// A player the server reserved a join for refuses to open the field battle because a side has no combatant on their
/// client. The battle the others already finished should conclude right away instead of waiting for that join to expire.
/// </summary>
public class FieldBattleOpenRefusedTests : MissionTestEnvironment
{
    private const string HostController = "host";
    private const string RefusingController = "refusing-player";

    public FieldBattleOpenRefusedTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RefusedFieldBattleOpen_ReleasesJoinSoFinishedBattleConcludes()
    {
        var (mapEventId, partyIds) = SetupCoopBattle(HostController, RefusingController);
        var clients = Clients.ToArray();
        var host = clients[0];
        var refusing = clients[1];
        RegisterPeer(host, HostController);
        RegisterPeer(refusing, RefusingController);
        EnterBattleWithMembership(host, HostController, mapEventId);

        // The mission start reserved a join for the refusing player, so the host's result waits for them.
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            refusing.NetPeer,
            new BattleJoinAccepted(mapEventId, RefusingController, Guid.NewGuid())));
        SendResult(host, mapEventId, BattleState.AttackerVictory);
        AssertMapEventPresent(mapEventId);

        refusing.Call(() =>
        {
            Assert.True(refusing.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var battle));
            Assert.True(refusing.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var localParty));
            // This client's copy has not received anyone on the attacker side yet.
            battle.AttackerSide._battleParties.Clear();

            WithMainParty(localParty, () => Assert.Null(CreateLauncher(refusing)
                .OpenCoopFieldBattle(new MissionInitializerRecord("battle_terrain_biome_012"))));
        }, VictoryConclusionDisabledMethods());

        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    public void RefusedFieldBattleOpen_FromAnotherPlayer_KeepsTheJoin()
    {
        var (mapEventId, _) = SetupCoopBattle(HostController, RefusingController);
        var clients = Clients.ToArray();
        var host = clients[0];
        var refusing = clients[1];
        RegisterPeer(host, HostController);
        RegisterPeer(refusing, RefusingController);
        EnterBattleWithMembership(host, HostController, mapEventId);

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            refusing.NetPeer,
            new BattleJoinAccepted(mapEventId, RefusingController, Guid.NewGuid())));
        SendResult(host, mapEventId, BattleState.AttackerVictory);

        // Only the player whose join it is can release it.
        host.Call(() => host.Resolve<INetwork>().SendAll(new NetworkFieldBattleOpenRefused(mapEventId)),
            VictoryConclusionDisabledMethods());

        AssertMapEventPresent(mapEventId);
    }

    private static CoopFieldBattleLauncher CreateLauncher(EnvironmentInstance client)
    {
        return new CoopFieldBattleLauncher(Mock.Of<IMessageBroker>(), client.ObjectManager,
            Mock.Of<ICoopBattleBehaviorAttacher>(), Mock.Of<IBattleAgentBudget>(), client.Resolve<ICoopBattleMissionRegistry>(),
            client.Resolve<INetwork>());
    }

    private static void WithMainParty(MobileParty party, Action action)
    {
        var campaign = Campaign.Current;
        var previousMainParty = campaign.MainParty;
        var previousEncounter = campaign.PlayerEncounter;
        try
        {
            campaign.MainParty = party;
            campaign.PlayerEncounter = null;
            action();
        }
        finally
        {
            campaign.MainParty = previousMainParty;
            campaign.PlayerEncounter = previousEncounter;
        }
    }

    private void RegisterPeer(EnvironmentInstance client, string controllerId)
    {
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer));
    }

    private void EnterBattleWithMembership(EnvironmentInstance client, string controllerId, string mapEventId)
    {
        EnterBattle(client, mapEventId);
        Server.SimulateMessage(client.NetPeer, new NetworkMissionEntered(controllerId, mapEventId));
    }

    private void SendResult(EnvironmentInstance client, string mapEventId, BattleState state)
    {
        client.Call(() =>
        {
            Assert.True(client.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out var assignment));
            client.Resolve<INetwork>().SendAll(
                new NetworkBattleResultReady(mapEventId, state, assignment.Epoch));
        }, VictoryConclusionDisabledMethods());
    }

    private void AssertMapEventPresent(string mapEventId)
    {
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _)));
    }

    private void AssertMapEventRemoved(string mapEventId)
    {
        Server.Call(() => Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _)));
    }

    private IReadOnlyList<MethodBase> VictoryConclusionDisabledMethods()
        => MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .Append(AccessTools.Method(typeof(MapEventRegistry), "CloseDestroyedMapEventEncounterIfNeeded"))
            .ToList();
}
