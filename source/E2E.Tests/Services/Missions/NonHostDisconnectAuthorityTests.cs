using Common.Messaging;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Agents.Packets;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public class NonHostDisconnectAuthorityTests : MissionTestEnvironment
{
    private const long InitialRevision = 4;
    private readonly Guid agentId = Guid.NewGuid();
    private readonly Guid mountId = Guid.NewGuid();

    public NonHostDisconnectAuthorityTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void NonHostDisconnect_ConvergesMovementAuthorityAcrossObserversRejoinsAndLaterMigrations()
    {
        using var engine = new MissionEngineFixture();
        var (mapEventId, partyIds) = SetupCoopBattle("A", "B", "C");
        string characterId = CreateRegisteredObject<CharacterObject>();
        var clients = Clients.ToArray();
        var a = CreateState(engine, clients[0], "A", mapEventId, characterId, partyIds[1]);
        var b = CreateState(engine, clients[1], "B", mapEventId, characterId, partyIds[1]);
        var c = CreateState(engine, clients[2], "C", mapEventId, characterId, partyIds[1]);
        try
        {
            foreach (var state in new[] { a, b, c })
                AnnounceEntry(state.Id, mapEventId, a, b, c);
            var originalPacket = CaptureMovement(b);

            Disconnect(b, mapEventId, a, c);
            AssertAuthority(a, "A", 5, AgentControllerType.AI);
            AssertAuthority(c, "A", 5, AgentControllerType.None);
            AssertMovementArrives(a, c);

            Rejoin(b, mapEventId, a, c);
            AssertAuthority(b, "A", 5, AgentControllerType.None);
            AssertMovementArrives(a, b, c);

            Disconnect(a, mapEventId, b, c);
            AssignHost("C", 2, mapEventId, b, c);
            AssertAuthority(b, "C", 6, AgentControllerType.None);
            AssertAuthority(c, "C", 6, AgentControllerType.AI);
            AssertMovementArrives(c, b);

            Rejoin(a, mapEventId, c, b);
            AssertAuthority(a, "C", 6, AgentControllerType.None);
            Disconnect(c, mapEventId, a, b);
            AssignHost("B", 3, mapEventId, a, b);
            AssertAuthority(a, "B", 7, AgentControllerType.None);
            AssertAuthority(b, "B", 7, AgentControllerType.AI);

            a.Instance.Call(() =>
            {
                GetMirror(a, agentId).MovementDirection = Vec2.Zero;
                Assert.True(a.Registry.TryGetAgentInfo(agentId, out var info));
                a.Controller.AgentMovementHandler.Interpolator.Forget(info.Agent);
            });
            b.Instance.Call(() => b.Network.Send("A", originalPacket));
            a.Instance.Call(() =>
            {
                a.Controller.AgentMovementHandler.Interpolator.Tick(1f / 60f);
                Assert.Equal(Vec2.Zero, GetMirror(a, agentId).MovementDirection);
            });
            AssertMovementArrives(b, a);
        }
        finally
        {
            foreach (var state in new[] { a, b, c })
                state.Instance.Call(BattleSpawnGate.EndBattle);
        }
    }

    private ClientState CreateState(
        MissionEngineFixture engine,
        EnvironmentInstance instance,
        string id,
        string mapEventId,
        string characterId,
        string partyId)
    {
        ClientState state = null;
        instance.Call(() =>
        {
            MockMission mission = CreateConnectedMission(engine, instance, mapEventId);
            var controller = instance.Resolve<CoopBattleController>();
            controller.Session.TryBegin(mapEventId);
            BattleSpawnGate.BeginBattle(mapEventId);
            instance.Resolve<IBattleHostRegistry>().Set(
                mapEventId, new BattleHostAssignment("A", new[] { "B", "C" }, 1));
            Assert.True(instance.ObjectManager.TryGetObject(partyId, out MobileParty party));
            var mapEventParty = party.Party.MapEventSide.Parties.Single(value => value.Party == party.Party);
            Assert.True(instance.ObjectManager.TryGetId(mapEventParty, out var mapEventPartyId));
            mission.SpawnMounted = true;
            var spawn = new BattleAgentSpawnData(
                agentId, characterId, Vec3.Zero, BattleSideEnum.Defender, 100f, "B",
                mapEventPartyId, 0, new Equipment(), default, null,
                mountAgentId: mountId, movementId: 1, mountMovementId: 2,
                authorityRevision: InitialRevision, mountAuthorityRevision: InitialRevision);
            instance.Resolve<IMessageBroker>().Publish(this, new NetworkSpawnBattleAgents(new[] { spawn }));
            state = new ClientState(id, instance, mission, controller);
            AssertAuthority(state, "B", InitialRevision,
                id == "B" ? AgentControllerType.AI : AgentControllerType.None);
        });
        return state;
    }

    private static void AnnounceEntry(string id, string mapEventId, params ClientState[] recipients)
    {
        foreach (var recipient in recipients)
            recipient.Instance.Call(() => recipient.Instance.Resolve<IMessageBroker>().Publish(
                recipient, new NetworkMissionPeerEntered(id, mapEventId)));
    }

    private static void Disconnect(ClientState departed, string mapEventId, params ClientState[] remaining)
    {
        departed.Instance.Call(departed.Network.Stop);
        foreach (var recipient in remaining)
            recipient.Instance.Call(() => recipient.Instance.Resolve<IMessageBroker>().Publish(
                recipient, new MissionPeerDisconnected(departed.Id, mapEventId)));
    }

    private void Rejoin(ClientState returning, string mapEventId, params ClientState[] remaining)
    {
        BattleHostAssignment assignment = null;
        remaining[0].Instance.Call(() => Assert.True(remaining[0].Instance.Resolve<IBattleHostRegistry>()
            .TryGet(mapEventId, out assignment)));
        returning.Instance.Call(() =>
        {
            foreach (Guid id in new[] { agentId, mountId })
            {
                Assert.True(returning.Registry.TryGetAgentInfo(id, out var old));
                returning.Mission.DeleteAgent(old.Agent);
                returning.Registry.RemoveAgent(id);
            }
            returning.Instance.Resolve<IMessageBroker>().Publish(returning,
                new NetworkBattleHostAssigned(mapEventId, assignment.HostControllerId,
                    assignment.SuccessorControllerIds.ToArray(), assignment.Epoch));
            returning.Mission.SpawnMounted = true;
            returning.Network.Start();
            returning.Network.ConnectToInstance(mapEventId);
        });
        AnnounceEntry(returning.Id, mapEventId, remaining);
    }

    private static void AssignHost(string hostId, int epoch, string mapEventId, params ClientState[] remaining)
    {
        string[] successors = remaining.Where(state => state.Id != hostId).Select(state => state.Id).ToArray();
        foreach (var recipient in remaining)
            recipient.Instance.Call(() => recipient.Instance.Resolve<IMessageBroker>().Publish(
                recipient, new NetworkBattleHostAssigned(mapEventId, hostId, successors, epoch)));
    }

    private void AssertAuthority(ClientState state, string authority, long revision, AgentControllerType controller)
    {
        state.Instance.Call(() =>
        {
            foreach (Guid id in new[] { agentId, mountId })
            {
                Assert.True(state.Registry.TryGetAgentInfo(id, out var info));
                Assert.Equal(authority, info.CurrentAuthority);
                Assert.Equal(revision, info.AuthorityRevision);
                Assert.Equal("B", info.OriginalOwner);
                Assert.Equal("B", info.MovementScopeId);
            }
            Assert.Equal(controller, GetMirror(state, agentId).Controller);
            Assert.Equal(AgentControllerType.None, GetMirror(state, mountId).Controller);
        });
    }

    private MovementPacket CaptureMovement(ClientState state)
    {
        MovementPacket packet = default;
        state.Instance.Call(() =>
        {
            Assert.True(state.Registry.TryGetAgentInfo(agentId, out var info));
            GetMirror(state, agentId).MovementDirection = new Vec2(1f, 0f);
            GetMirror(state, mountId).MovementDirection = new Vec2(1f, 0f);
            packet = new MovementPacket(new[] { agentId }, new[] { new AgentData(info.Agent) },
                state.Id, new[] { info.AuthorityRevision });
        });
        return packet;
    }

    private void AssertMovementArrives(ClientState sender, params ClientState[] recipients)
    {
        MovementPacket riderPacket = CaptureMovement(sender);
        MountMovementPacket mountPacket = default;
        sender.Instance.Call(() =>
        {
            Assert.True(sender.Registry.TryGetAgentInfo(mountId, out var info));
            GetMirror(sender, mountId).MovementDirection = new Vec2(1f, 0f);
            mountPacket = new MountMovementPacket(new[] { mountId },
                new[] { new AgentMountData(info.Agent, mountId) }, sender.Id, new[] { info.AuthorityRevision });
        });
        foreach (var recipient in recipients)
        {
            recipient.Instance.Call(() =>
            {
                GetMirror(recipient, agentId).MovementDirection = Vec2.Zero;
                GetMirror(recipient, mountId).MovementDirection = Vec2.Zero;
            });
            sender.Instance.Call(() =>
            {
                sender.Network.Send(recipient.Id, riderPacket);
                sender.Network.Send(recipient.Id, mountPacket);
            });
            recipient.Instance.Call(() =>
            {
                Assert.True(recipient.Registry.TryGetAgentInfo(agentId, out var rider));
                var interpolator = recipient.Controller.AgentMovementHandler.Interpolator;
                Assert.True(interpolator.TryGetTargetFrame(rider.Agent, out _, out _, out _));
                interpolator.Tick(1f / 60f);
                Assert.Equal(new Vec2(1f, 0f), GetMirror(recipient, agentId).MovementDirection);
                Assert.Equal(new Vec2(1f, 0f), GetMirror(recipient, mountId).MovementDirection);
            });
        }
    }

    private static MirrorAgent GetMirror(ClientState state, Guid id)
    {
        Assert.True(state.Registry.TryGetAgentInfo(id, out var info));
        Assert.True(AgentMirror.TryGet(info.Agent, out var mirror));
        return mirror;
    }

    private sealed class ClientState
    {
        public string Id { get; }
        public EnvironmentInstance Instance { get; }
        public MockMission Mission { get; }
        public CoopBattleController Controller { get; }
        public INetworkAgentRegistry Registry => Instance.Resolve<INetworkAgentRegistry>();
        public MockBattleNetwork Network => Instance.Resolve<MockBattleNetwork>();

        public ClientState(string id, EnvironmentInstance instance, MockMission mission, CoopBattleController controller)
        {
            Id = id;
            Instance = instance;
            Mission = mission;
            Controller = controller;
        }
    }
}
