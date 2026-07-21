using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Instances.Handlers;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using HarmonyLib;
using Missions.Messages;
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
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>Tests BR-005 result-ready finalization before another player can join an ended battle.</summary>
public class BattleResultReadyTests : MissionTestEnvironment
{
    public BattleResultReadyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void CoopMissionVictory_DoesNotSendLegacyImmediateConclusion()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "campaign-player");
        var host = Clients.First();

        host.Call(() =>
        {
            Assert.True(host.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            host.NetworkSentMessages.Clear();
            bool wasInCoopBattleMission = BattleConclusionGate.IsInCoopBattleMission;
            BattleConclusionGate.IsInCoopBattleMission = true;
            try
            {
                mapEvent.BattleState = BattleState.DefenderVictory;
            }
            finally
            {
                BattleConclusionGate.IsInCoopBattleMission = wasInCoopBattleMission;
            }
        }, VictoryConclusionDisabledMethods());

        Assert.Empty(host.NetworkSentMessages.GetMessages<NetworkChangeBattleState>());
        AssertMapEventPresent(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void SoleMissionMemberReportsDefeat_FinalizesBeforeMissionLeave()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "campaign-player");
        var host = Clients.First();
        RegisterPeer(host, "host");
        EnterBattleWithMembership(host, "host", mapEventId);

        var concluded = 0;
        Server.Resolve<IMessageBroker>().Subscribe<MapEventConcluded>(_ => concluded++);

        SendResult(host, mapEventId, BattleState.DefenderVictory);

        Assert.Equal(1, concluded);
        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void HostResult_WaitsForOtherMissionMemberThenFinalizes()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "successor");
        var clients = Clients.ToArray();
        RegisterPeer(clients[0], "host");
        RegisterPeer(clients[1], "successor");
        EnterBattleWithMembership(clients[0], "host", mapEventId);
        EnterBattleWithMembership(clients[1], "successor", mapEventId);

        SendResult(clients[0], mapEventId, BattleState.AttackerVictory);
        AssertMapEventPresent(mapEventId);

        SendResult(clients[1], mapEventId, BattleState.AttackerVictory);
        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void HostResult_DoesNotFinalizeWhileAnotherMemberIsLoading()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "loading-player");
        var clients = Clients.ToArray();
        RegisterPeer(clients[0], "host");
        RegisterPeer(clients[1], "loading-player");
        EnterBattleWithMembership(clients[0], "host", mapEventId);
        EnterBattleWithMembership(clients[1], "loading-player", mapEventId, missionReady: false);

        SendResult(clients[0], mapEventId, BattleState.AttackerVictory);

        AssertMapEventPresent(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void HostResult_WaitsForAcceptedJoinerBeforeMissionEntry()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "battle-opponent");
        var joinerPartyId = CreateRegisteredObject<MobileParty>();
        var joinerHeroId = CreateRegisteredObject<Hero>();
        string? joinerPartyBaseId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerPartyId, out var joinerParty));
            Assert.True(Server.ObjectManager.TryGetId(joinerParty.Party, out joinerPartyBaseId));
        });
        RegisterAsPlayerParty("joining-player", joinerHeroId, joinerPartyId);

        var clients = Clients.ToArray();
        SetControllerId(clients[1], "joining-player");
        RegisterPeer(clients[0], "host");
        RegisterPeer(clients[1], "joining-player");
        EnterBattleWithMembership(clients[0], "host", mapEventId);
        bool? reservationPrecededJoinCommit = null;
        Server.Resolve<IMessageBroker>().Subscribe<BattleJoinAccepted>(payload =>
        {
            if (payload.What.ControllerId != "joining-player" || reservationPrecededJoinCommit != null)
                return;

            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(joinerPartyBaseId!, out var joinerParty));
            reservationPrecededJoinCommit = joinerParty.MapEventSide == null;
        });

        clients[1].Call(() => clients[1].Resolve<INetwork>().SendAll(
            new NetworkRequestJoinBattle(mapEventId, joinerPartyBaseId!, BattleSideEnum.Attacker)));

        Assert.True(reservationPrecededJoinCommit);
        SendResult(clients[0], mapEventId, BattleState.AttackerVictory);
        AssertMapEventPresent(mapEventId);

        EnterBattleWithMembership(clients[1], "joining-player", mapEventId);
        SendResult(clients[1], mapEventId, BattleState.AttackerVictory);

        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void HostResult_FinalizesWhenLoadingMemberDeparts()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "loading-player");
        var clients = Clients.ToArray();
        RegisterPeer(clients[0], "host");
        RegisterPeer(clients[1], "loading-player");
        EnterBattleWithMembership(clients[0], "host", mapEventId);
        EnterBattleWithMembership(clients[1], "loading-player", mapEventId, missionReady: false);

        SendResult(clients[0], mapEventId, BattleState.AttackerVictory);
        AssertMapEventPresent(mapEventId);

        Server.Call(
            () => Server.SimulateMessage(
                clients[1].NetPeer,
                new NetworkMissionLeft("loading-player", mapEventId)),
            VictoryConclusionDisabledMethods());

        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void HostResult_FinalizesWhenAcceptedJoinerIsCancelled()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "battle-opponent");
        var host = Clients.First();
        RegisterPeer(host, "host");
        EnterBattleWithMembership(host, "host", mapEventId);

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            host.NetPeer,
            new BattleJoinAccepted(mapEventId, "cancelled-joiner")));
        SendResult(host, mapEventId, BattleState.AttackerVictory);
        AssertMapEventPresent(mapEventId);

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(
            this,
            new BattleJoinCancelled(mapEventId, "cancelled-joiner")), VictoryConclusionDisabledMethods());

        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void HostResult_FinalizesWhenAcceptedJoinerReservationExpires()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "battle-opponent");
        var host = Clients.First();
        RegisterPeer(host, "host");
        EnterBattleWithMembership(host, "host", mapEventId);

        Server.Call(() =>
        {
            var handler = Server.Resolve<ServerBattleCompletionHandler>();
            handler.JoinReservationTimeout = TimeSpan.Zero;
            Server.Resolve<IMessageBroker>().Publish(
                host.NetPeer,
                new BattleJoinAccepted(mapEventId, "stalled-joiner"));
        });
        SendResult(host, mapEventId, BattleState.AttackerVictory);
        AssertMapEventPresent(mapEventId);

        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new CampaignTick()),
            VictoryConclusionDisabledMethods());

        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void ResolvedMembersLeaveBeforePromotionReplay_FinalizesFromAgreedReports()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "successor");
        var clients = Clients.ToArray();
        RegisterPeer(clients[0], "host");
        RegisterPeer(clients[1], "successor");
        EnterBattleWithMembership(clients[0], "host", mapEventId);
        EnterBattleWithMembership(clients[1], "successor", mapEventId);

        Server.Call(() =>
        {
            Server.SimulateMessage(
                clients[0].NetPeer,
                new NetworkBattleResultReady(mapEventId, BattleState.DefenderVictory, hostEpoch: 1));
            Server.SimulateMessage(
                clients[0].NetPeer,
                new NetworkMissionLeft("host", mapEventId));
            Server.SimulateMessage(
                clients[1].NetPeer,
                new NetworkBattleResultReady(mapEventId, BattleState.DefenderVictory, hostEpoch: 2));
            Server.SimulateMessage(
                clients[1].NetPeer,
                new NetworkMissionLeft("successor", mapEventId));
        }, VictoryConclusionDisabledMethods());

        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void SuccessorMustReportAgainAfterBecomingHost()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "successor");
        var clients = Clients.ToArray();
        RegisterPeer(clients[0], "host");
        RegisterPeer(clients[1], "successor");
        EnterBattleWithMembership(clients[0], "host", mapEventId);
        EnterBattleWithMembership(clients[1], "successor", mapEventId);

        SendResult(clients[1], mapEventId, BattleState.DefenderVictory);
        AssertMapEventPresent(mapEventId);

        Server.Call(
            () => Server.SimulateMessage(
                clients[0].NetPeer,
                new NetworkMissionLeft("host", mapEventId)),
            VictoryConclusionDisabledMethods());

        AssertMapEventPresent(mapEventId);
        SendResult(clients[1], mapEventId, BattleState.DefenderVictory);
        AssertMapEventRemoved(mapEventId);
    }

    [Fact]
    [Trait("Requirement", "BR-005")]
    public void DirectJoinRequest_ConcludedMapEvent_DoesNotAddParty()
    {
        var (mapEventId, _) = SetupCoopBattle("host", "other-player");
        var joinerId = CreateRegisteredObject<MobileParty>();
        string? joinerPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joinerId, out var joiner));
            Assert.True(Server.ObjectManager.TryGetId(joiner.Party, out joinerPartyId));
            mapEvent._battleState = BattleState.AttackerVictory;
        });

        var client = Clients.First();
        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestJoinBattle(mapEventId, joinerPartyId!, BattleSideEnum.Attacker)));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<PartyBase>(joinerPartyId, out var joinerParty));
            Assert.Null(joinerParty.MapEventSide);
        });
    }

    private void RegisterPeer(EnvironmentInstance client, string controllerId)
    {
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer));
    }

    private void EnterBattleWithMembership(
        EnvironmentInstance client,
        string controllerId,
        string mapEventId,
        bool missionReady = true)
    {
        EnterBattle(client, mapEventId, missionReady);
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
