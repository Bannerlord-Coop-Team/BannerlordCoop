using Autofac;
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Services.Missions;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.Sessions.Messages;
using HarmonyLib;
using Missions;
using Missions.Agents;
using Missions.Battles;
using Missions.Data;
using Missions.Messages;
using Missions.Sessions;
using Missions.Sessions.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Sessions;

/// <summary>
/// The session composition root on the headless mission engine: two clients, a session id from
/// <see cref="SessionInstanceId"/> and a troop that is a <see cref="BasicCharacterObject"/>, so nothing
/// reads the campaign.
/// </summary>
public class CoopSessionControllerTests : MissionTestEnvironment
{
    public CoopSessionControllerTests(ITestOutputHelper output) : base(output) { }

    private static CoopSessionController CreateController(EnvironmentInstance client, string sessionId)
        => client.Container.Resolve<CoopSessionController>(
            new TypedParameter(typeof(SessionMissionContext), new SessionMissionContext(sessionId)));

    // The StringId setter is intercepted in the test engine and drops the value, so the backing field is set directly.
    private static BasicCharacterObject CreateTroop(string stringId)
    {
        var troop = new BasicCharacterObject();
        AccessTools.Field(typeof(MBObjectBase), "<StringId>k__BackingField").SetValue(troop, stringId);
        return troop;
    }

    private static void RegisterTroop(BasicCharacterObject troop)
    {
        var objects = MBObjectManager.Instance;
        objects.RegisterType<BasicCharacterObject>("BasicCharacterObject", "BasicCharacterObjects", 9001, true, false);
        objects.RegisterObject(troop);
    }

    private (EnvironmentInstance owner, EnvironmentInstance peer, string sessionId, CoopSessionController ownerController,
        CoopSessionController peerController, MockMission ownerMission, MockMission peerMission) StartTwoMembers(MissionEngineFixture fixture)
    {
        var owner = Clients.First();
        var peer = Clients.Skip(1).First();
        SetControllerId(owner, "owner");
        SetControllerId(peer, "peer");
        var sessionId = SessionInstanceId.Create();
        CoopSessionController ownerController = null!, peerController = null!;
        MockMission ownerMission = null!, peerMission = null!;

        peer.Call(() =>
        {
            peerMission = CreateConnectedMission(fixture, peer, sessionId);
            peerController = CreateController(peer, sessionId);
            peerController.OnBehaviorInitialize();
            BattleSpawnGate.BeginBattle(sessionId);
        });
        owner.Call(() =>
        {
            ownerMission = CreateConnectedMission(fixture, owner, sessionId);
            ownerController = CreateController(owner, sessionId);
            ownerController.OnBehaviorInitialize();
            BattleSpawnGate.BeginBattle(sessionId);
        });
        return (owner, peer, sessionId, ownerController, peerController, ownerMission, peerMission);
    }

    [Fact]
    public void Controller_ResolvesWithSessionContext()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();

            Assert.Equal(sessionId, controller.Session.InstanceId);
            Assert.Single(client.NetworkSentMessages.GetMessages<NetworkMissionEntered>(),
                m => m.InstanceId == sessionId && m.ControllerId == "owner");
            GC.KeepAlive(controller);
        });
    }

    // The mod creates its Game after the session state, so the mission registers the items that Game loaded.
    [Fact]
    public void OnBehaviorInitialize_RegistersTheItemsOfTheModsGame()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();
        var objectManager = client.Container.Resolve<GameInterface.Services.ObjectManager.IObjectManager>();

        client.Call(() =>
        {
            var spear = MBObjectManager.Instance.GetObject<ItemObject>("linen");
            Assert.NotNull(spear);
            objectManager.Remove(spear);
            Assert.False(objectManager.TryGetId(spear, out _));

            CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();

            Assert.True(objectManager.TryGetId(spear, out _));
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void OwnedBasicCharacterSpawn_ReplicatesToPeer()
    {
        using var fixture = new MissionEngineFixture();
        var (owner, peer, sessionId, ownerController, peerController, ownerMission, peerMission) = StartTwoMembers(fixture);
        var troop = CreateTroop("taom_test_session_spearman");
        peer.Call(() => RegisterTroop(troop));

        owner.Call(() =>
        {
            var agent = ownerMission.SpawnAgent(new AgentBuildData(troop)
                .Controller(AgentControllerType.AI).TroopOrigin(new BasicBattleAgentOrigin(troop)).Team(ownerMission.DefenderTeam.Shell));
            owner.Resolve<IMessageBroker>().Publish(this, new AgentSpawnedInBattle(agent));
            ownerController.OnMissionTick(0.1f);
        });
        peer.Call(() => peerController.OnMissionTick(0.1f));

        peer.Call(() =>
        {
            var puppet = Assert.Single(peer.Resolve<INetworkAgentRegistry>().GetAgents("owner"));
            Assert.Equal("taom_test_session_spearman", puppet.Agent.Character.StringId);
        });
    }

    [Fact]
    public void PartylessPuppet_Spawns()
    {
        using var fixture = new MissionEngineFixture();
        var (owner, peer, sessionId, ownerController, peerController, ownerMission, peerMission) = StartTwoMembers(fixture);
        var troop = CreateTroop("taom_test_session_archer");
        peer.Call(() => RegisterTroop(troop));

        owner.Call(() =>
        {
            var agent = ownerMission.SpawnAgent(new AgentBuildData(troop)
                .Controller(AgentControllerType.AI).TroopOrigin(new BasicBattleAgentOrigin(troop)).Team(ownerMission.DefenderTeam.Shell));
            owner.Resolve<IMessageBroker>().Publish(this, new AgentSpawnedInBattle(agent));
            ownerController.OnMissionTick(0.1f);
        });
        peer.Call(() => peerController.OnMissionTick(0.1f));

        peer.Call(() =>
        {
            var puppet = Assert.Single(peer.Resolve<INetworkAgentRegistry>().GetAgents("owner"));
            var origin = Assert.IsType<SessionPuppetAgentOrigin>(puppet.Agent.Origin);
            Assert.Equal(peerMission.DefenderTeam.Shell.Color, origin.FactionColor);
        });
    }

    [Fact]
    public void AfterStart_SendsSessionHostRequest_NotBattleRequest()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();

            controller.AfterStart();

            Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestSessionHost>(), m => m.SessionId == sessionId);
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestBattleHost>());
            Assert.Empty(client.InternalMessages.GetMessages<BattleMissionReady>());
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void HostAssignment_PublishesSessionHostChanged()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();
            var changes = new List<SessionHostChanged>();
            var broker = client.Resolve<IMessageBroker>();
            broker.Subscribe<SessionHostChanged>(payload => changes.Add(payload.What));

            broker.Publish(this, new NetworkBattleHostAssigned(sessionId, "owner", System.Array.Empty<string>(), 1));

            var change = Assert.Single(changes);
            Assert.Equal(sessionId, change.SessionId);
            Assert.Equal("owner", change.HostControllerId);
            Assert.Equal(1, change.Epoch);
            Assert.True(change.IsLocalHost);
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void SuccessorChange_SameHostAndEpoch_DoesNotRepublishHostChanged()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();
            var changes = new List<SessionHostChanged>();
            var broker = client.Resolve<IMessageBroker>();
            broker.Subscribe<SessionHostChanged>(payload => changes.Add(payload.What));

            broker.Publish(this, new NetworkBattleHostAssigned(sessionId, "owner", System.Array.Empty<string>(), 1));
            broker.Publish(this, new NetworkBattleHostAssigned(sessionId, "owner", new[] { "peer" }, 1));

            Assert.Single(changes);

            broker.Publish(this, new NetworkBattleHostAssigned(sessionId, "peer", System.Array.Empty<string>(), 2));

            Assert.Equal(2, changes.Count);
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void EndBattleMissionForSession_PublishesEndRequested()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();
            var ends = new List<SessionMissionEndRequested>();
            var broker = client.Resolve<IMessageBroker>();
            broker.Subscribe<SessionMissionEndRequested>(payload => ends.Add(payload.What));

            broker.Publish(this, new NetworkEndBattleMission(sessionId));

            Assert.Equal(sessionId, Assert.Single(ends).SessionId);
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void EndBattleMissionForOtherId_Ignored()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();
            var ends = new List<SessionMissionEndRequested>();
            var broker = client.Resolve<IMessageBroker>();
            broker.Subscribe<SessionMissionEndRequested>(payload => ends.Add(payload.What));

            broker.Publish(this, new NetworkEndBattleMission(SessionInstanceId.Create()));

            Assert.Empty(ends);
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void NonHost_TryRequestClose_SendsNothing()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();

            Assert.False(controller.TryRequestClose());
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestSessionClose>());

            client.Resolve<IBattleHostRegistry>().Set(sessionId, new BattleHostAssignment("owner", System.Array.Empty<string>(), 3));
            Assert.True(controller.TryRequestClose());
            var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestSessionClose>());
            Assert.Equal(sessionId, request.SessionId);
            Assert.Equal(3, request.HostEpoch);
            GC.KeepAlive(controller);
        });
    }

    [Fact]
    public void SessionTick_WithBasicPlayerTroop_DoesNotThrow()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var sessionId = SessionInstanceId.Create();

        client.Call(() =>
        {
            var mission = CreateConnectedMission(fixture, client, sessionId);
            var controller = CreateController(client, sessionId);
            controller.OnBehaviorInitialize();
            var troop = CreateTroop("taom_test_session_lord");
            var previousTroop = Game.Current.PlayerTroop;
            Game.Current.PlayerTroop = troop;
            try
            {
                BattleSpawnGate.BeginBattle(sessionId);
                mission.SpawnAgent(new AgentBuildData(troop)
                    .Controller(AgentControllerType.AI).TroopOrigin(new BasicBattleAgentOrigin(troop)).Team(mission.DefenderTeam.Shell));

                controller.OnMissionTick(0.1f);
                controller.OnPreDisplayMissionTick(0.1f);
            }
            finally
            {
                Game.Current.PlayerTroop = previousTroop;
                BattleSpawnGate.EndBattle();
            }
            GC.KeepAlive(controller);
        });
    }
}
