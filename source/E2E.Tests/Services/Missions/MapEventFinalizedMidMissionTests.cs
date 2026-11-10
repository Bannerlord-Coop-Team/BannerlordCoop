using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Instances;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// #4141 and #3629: the server finalizing a battle its players are still fighting (a peace decision, say) ends their
/// missions, instead of letting them finish a battle whose result the server then throws away.
/// </summary>
public class MapEventFinalizedMidMissionTests : MissionTestEnvironment
{
    private static readonly string[] ControllerIds = { "host", "successor" };

    public MapEventFinalizedMidMissionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ServerFinalizesBattle_WhilePlayersFightInItsMission_EndsTheirMissions()
    {
        var (mapEventId, _) = SetupCoopBattle(ControllerIds);
        var clients = Clients.ToArray();
        using var fixture = new MissionEngineFixture();
        var missions = clients.Select((client, i) => EnterBattleMission(fixture, client, ControllerIds[i], mapEventId)).ToArray();
        var notices = clients.Select(RecordNotices).ToArray();
        AssertHost(clients[0], mapEventId, "host", "successor");

        FinalizeOnServer(mapEventId);

        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkEndBattleMission>().Count());
        for (int i = 0; i < clients.Length; i++)
        {
            Assert.True(missions[i].EndMissionCalled, $"{ControllerIds[i]} is still in the finalized battle's mission");
            Assert.Single(notices[i]);
        }
    }

    [Fact]
    public void PlayersConcludeTheirBattle_KeepTheirMissionsForTheResults()
    {
        var (mapEventId, _) = SetupCoopBattle(ControllerIds);
        var clients = Clients.ToArray();
        using var fixture = new MissionEngineFixture();
        var missions = clients.Select((client, i) => EnterBattleMission(fixture, client, ControllerIds[i], mapEventId)).ToArray();

        foreach (var client in clients)
        {
            client.Call(() =>
            {
                Assert.True(client.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out var assignment));
                client.Resolve<INetwork>().SendAll(
                    new NetworkBattleResultReady(mapEventId, BattleState.AttackerVictory, assignment.Epoch));
            }, VictoryConclusionDisabledMethods());
        }

        Server.Call(() => Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _)));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkEndBattleMission>());
        Assert.All(missions, mission => Assert.False(mission.EndMissionCalled));
    }

    // A late end message for a battle the player already left must not end the mission it is in now.
    [Fact]
    public void EndMessageForAnotherBattle_LeavesTheCurrentMissionRunning()
    {
        var (mapEventId, _) = SetupCoopBattle(ControllerIds);
        var client = Clients.First();
        using var fixture = new MissionEngineFixture();
        var mission = OpenBattleMission(fixture, client, "battle-the-player-is-in-now");
        var notices = RecordNotices(client);

        client.SimulateMessage(Server.NetPeer, new NetworkEndBattleMission(mapEventId));

        Assert.False(mission.EndMissionCalled);
        Assert.Empty(notices);
    }

    // Native CheckMissionEnded sets MissionEnded when the result is ready but keeps the mission open on the scoreboard.
    [Theory]
    [InlineData(Mission.State.Continuing, true)]
    [InlineData(Mission.State.EndingNextFrame, false)]
    [InlineData(Mission.State.Over, false)]
    public void EndMessageAfterTheResultIsReady_EndsOnlyAMissionNotTearingDown(Mission.State state, bool expectEnded)
    {
        var (mapEventId, _) = SetupCoopBattle(ControllerIds);
        var client = Clients.First();
        using var fixture = new MissionEngineFixture();
        var mission = OpenBattleMission(fixture, client, mapEventId);
        mission.Shell._missionEnded = true;
        mission.Shell.CurrentState = state;

        client.SimulateMessage(Server.NetPeer, new NetworkEndBattleMission(mapEventId));

        Assert.Equal(expectEnded, mission.EndMissionCalled);
    }

    [Fact]
    public void ServerFinalizesBattle_WhileAJoinerIsStillEntering_EndsTheJoinersMission()
    {
        var (mapEventId, _) = SetupCoopBattle(ControllerIds);
        var (host, joiner) = (Clients.First(), Clients.Last());
        using var fixture = new MissionEngineFixture();
        EnterBattleMission(fixture, host, "host", mapEventId);
        AcceptJoin(joiner, "successor", mapEventId);
        var joinerMission = OpenBattleMission(fixture, joiner, mapEventId);

        FinalizeOnServer(mapEventId);

        Assert.True(joinerMission.EndMissionCalled);
    }

    // The joiner opens its mission only after the end message, so the refused entry has to tell it again.
    [Fact]
    public void JoinerEnteringAfterTheFinalize_IsRefusedAndLeavesTheMission()
    {
        var (mapEventId, _) = SetupCoopBattle(ControllerIds);
        var joiner = Clients.Last();
        using var fixture = new MissionEngineFixture();
        AcceptJoin(joiner, "successor", mapEventId);

        FinalizeOnServer(mapEventId);
        var joinerMission = OpenBattleMission(fixture, joiner, mapEventId);
        Server.SimulateMessage(joiner.NetPeer, new NetworkMissionEntered("successor", mapEventId));

        Assert.True(joinerMission.EndMissionCalled);
        Server.Call(() => Assert.False(Server.Resolve<IMissionManager>().TryGetControllers(mapEventId, out _)));
    }

    // The host can only finalize from its menu while it is host, and leaving the mission hands that to the successor.
    [Fact]
    public void HostLeavesForTheMenu_WhileTheSuccessorFights_SuccessorKeepsTheBattle()
    {
        var (mapEventId, _) = SetupCoopBattle(ControllerIds);
        var (host, successor) = (Clients.First(), Clients.Last());
        using var fixture = new MissionEngineFixture();
        EnterBattleMission(fixture, host, "host", mapEventId);
        var successorMission = EnterBattleMission(fixture, successor, "successor", mapEventId);

        Server.SimulateMessage(host.NetPeer, new NetworkMissionLeft("host", mapEventId));
        host.Call(() => host.Resolve<INetwork>().SendAll(new NetworkMapEventFinalizeAttempted(mapEventId)),
            MapEventDisabledMethods);

        AssertHost(Server, mapEventId, "successor");
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _)));
        Assert.False(successorMission.EndMissionCalled);
    }

    private void FinalizeOnServer(string mapEventId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            mapEvent.FinalizeEvent();
        }, MapEventDisabledMethods);
    }

    private MockMission EnterBattleMission(MissionEngineFixture fixture, EnvironmentInstance client, string controllerId, string mapEventId)
    {
        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer));
        EnterBattle(client, mapEventId);
        Server.SimulateMessage(client.NetPeer, new NetworkMissionEntered(controllerId, mapEventId));
        return OpenBattleMission(fixture, client, mapEventId);
    }

    private void AcceptJoin(EnvironmentInstance joiner, string controllerId, string mapEventId)
    {
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer(controllerId, joiner.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(joiner.NetPeer, new BattleJoinAccepted(mapEventId, controllerId, Guid.NewGuid()));
        });
    }

    // A headless mission carrying the battle logic that answers the server's end message for its own battle.
    private static MockMission OpenBattleMission(MissionEngineFixture fixture, EnvironmentInstance client, string instanceId)
    {
        MockMission? mission = null;
        client.Call(() =>
        {
            mission = fixture.CreateMission(client);
            var logic = new BattleResultReadyLogic(
                Mock.Of<IBattleResultCommitter>(),
                Mock.Of<ISiegeEngineStateReporter>(),
                client.Resolve<IMessageBroker>(),
                Mock.Of<IBattleSession>(session => session.InstanceId == instanceId),
                Mock.Of<IBattleDeploymentCoordinator>(),
                Mock.Of<INetwork>());
            logic.Mission = mission.Shell;
            mission.Shell.MissionBehaviors.Add(logic);
        });
        return mission!;
    }

    private static List<string> RecordNotices(EnvironmentInstance client)
    {
        var notices = new List<string>();
        client.Resolve<IMessageBroker>().Subscribe<SendInformationMessage>(payload => notices.Add(payload.What.Text));
        return notices;
    }
}
