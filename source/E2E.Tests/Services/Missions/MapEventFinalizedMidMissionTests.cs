using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.Players;
using Missions.Messages;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
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
        var missions = OpenBattleMissions(fixture, clients, mapEventId);
        var notices = clients.Select(RecordNotices).ToArray();
        AssertHost(clients[0], mapEventId, "host", "successor");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            mapEvent.FinalizeEvent();
        }, MapEventDisabledMethods);

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
        var missions = OpenBattleMissions(fixture, clients, mapEventId);

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

    private MockMission[] OpenBattleMissions(MissionEngineFixture fixture, EnvironmentInstance[] clients, string mapEventId)
    {
        var missions = new MockMission[clients.Length];
        for (int i = 0; i < clients.Length; i++)
        {
            var client = clients[i];
            var controllerId = ControllerIds[i];
            Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer));
            EnterBattle(client, mapEventId);
            Server.SimulateMessage(client.NetPeer, new NetworkMissionEntered(controllerId, mapEventId));
            MockMission? mission = null;
            client.Call(() => mission = fixture.CreateMission(client));
            missions[i] = mission!;
        }

        return missions;
    }

    private static List<string> RecordNotices(EnvironmentInstance client)
    {
        var notices = new List<string>();
        client.Resolve<IMessageBroker>().Subscribe<SendInformationMessage>(payload => notices.Add(payload.What.Text));
        return notices;
    }
}
