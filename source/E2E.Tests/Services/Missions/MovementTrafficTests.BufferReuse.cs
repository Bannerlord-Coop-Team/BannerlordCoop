using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

// Reused per-tick buffers must not change what is sent, or carry one tick's or peer's state into the next.
public partial class MovementTrafficTests
{
    [Fact]
    public void PollMovement_KeepsBufferCapacitiesAndLeavesNothingBehindBetweenTicks()
    {
        var states = new List<(int OwnedAgents, int Captured, int RecipientIds, int Retained)>();

        RunBufferReuseScenario(states);

        Assert.Equal(12, states.Count);
        Assert.All(states, state => Assert.Equal(0, state.Retained));
        // A list that was never filled stays at capacity 0, so this also fails if a buffer goes back to a local.
        Assert.True(states[0].OwnedAgents >= 8, $"owned agents capacity was {states[0].OwnedAgents}");
        Assert.True(states[0].Captured >= 5, $"captured capacity was {states[0].Captured}");
        Assert.True(states[0].RecipientIds >= 3, $"recipient capacity was {states[0].RecipientIds}");
        // Eight agents are owned from the first tick, so every list is already at its final size.
        Assert.All(states, state => Assert.Equal(states[0], state));
    }

    [Fact]
    public void PollMovement_DoesNotSendDepartedPeersOrRemovedAgentsFromTheLastTick()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            foreach (string receiver in new[] { "r1", "r2", "r3" })
                broker.Publish(this, new NetworkMissionPeerEntered(receiver, "battle"));

            var registry = peer.Resolve<INetworkAgentRegistry>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            using var handler = CreateBufferReuseHandler(peer, network, registry, broker);
            var mirrors = new List<MirrorAgent>();
            for (int i = 1; i <= 4; i++)
            {
                Agent rider = SpawnRider(mock);
                Assert.True(AgentMirror.TryGet(rider, out MirrorAgent mirror));
                mirrors.Add(mirror);
                Assert.True(registry.TryRegisterAgent("owner", ScenarioId(i), (ushort)i, rider));
            }

            handler.PollMovement(0f);
            foreach (string receiver in new[] { "r1", "r2", "r3" })
                Assert.Equal(4, SentAgentCount(network, receiver));
            network.DirectPacketSends.Clear();

            broker.Publish(this, new MissionPeerLeft("r2", "battle"));
            broker.Publish(this, new MissionPeerLeft("r3", "battle"));
            Assert.True(registry.RemoveAgent(ScenarioId(3)));
            Assert.True(registry.RemoveAgent(ScenarioId(4)));
            mirrors[0].Position = new Vec3(5f, 0f, 0f);
            mirrors[1].Position = new Vec3(6f, 0f, 0f);

            handler.PollMovement(0.026f);

            Assert.Equal(2, SentAgentCount(network, "r1"));
            Assert.Equal(0, SentAgentCount(network, "r2"));
            Assert.Equal(0, SentAgentCount(network, "r3"));
        });
    }

    private static int SentAgentCount(MockBattleNetwork network, string receiver) =>
        network.DirectPacketSends
            .Where(send => send.ControllerId == receiver)
            .Select(send => send.Packet)
            .OfType<MovementPacket>()
            .Sum(packet => packet.Agents.Length);

    private AgentMovementHandler CreateBufferReuseHandler(
        EnvironmentInstance peer,
        MockBattleNetwork network,
        INetworkAgentRegistry registry,
        IMessageBroker broker)
    {
        peer.Resolve<ICoopMissionComponent>().AgentMovementHandler.Dispose();
        var rateController = new Mock<IMovementRateController>();
        rateController
            .Setup(value => value.AdvanceFrame(It.IsAny<float>()))
            .Returns(new MovementCadence(40, 40));
        rateController
            .Setup(value => value.GetReceiverCapHz(It.IsAny<string>()))
            .Returns<string>(receiver => receiver == "r3" ? 20 : 40);
        rateController
            .Setup(value => value.GetReceiverIncomingBytesPerSecond(It.IsAny<string>()))
            .Returns(1024 * 1024d);
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        return new AgentMovementHandler(
            network,
            peer.Resolve<IPacketManager>(),
            broker,
            registry,
            peer.Resolve<IControllerIdProvider>(),
            peer.Resolve<IAgentEquipmentApplier>(),
            new MovementBatchSender(
                network,
                new MovementPacketCompressor(serializer),
                () => new MovementTrafficBudget()),
            peer.Resolve<IPuppetMountStateRepairer>(),
            peer.Resolve<IAgentVisualActionAccessor>(),
            rateController.Object,
            peer.Resolve<IMovementPriorityScheduler>(),
            peer.Resolve<IMissionContext>(),
            peer.Resolve<INavalDeckMovementProbe>(),
            peer.Resolve<ILocationNpcAuthorityState>());
    }

    // Twelve ticks to three receivers (one with a lower cap) with a main agent, a ridden and a standalone mount,
    // an agent removed and another registered, and an authority transfer out and back.
    private void RunBufferReuseScenario(
        List<(int OwnedAgents, int Captured, int RecipientIds, int Retained)> bufferStates)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner");

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            foreach (string receiver in new[] { "r1", "r2", "r3" })
                broker.Publish(this, new NetworkMissionPeerEntered(receiver, "battle"));

            var registry = peer.Resolve<INetworkAgentRegistry>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            using var handler = CreateBufferReuseHandler(peer, network, registry, broker);

            var riders = new List<MirrorAgent>();
            var riderAgents = new List<Agent>();
            for (int i = 1; i <= 6; i++)
            {
                Agent rider = SpawnRider(mock);
                if (i == 1) mock.MainAgent = rider;
                Assert.True(AgentMirror.TryGet(rider, out MirrorAgent mirror));
                riders.Add(mirror);
                riderAgents.Add(rider);
                Assert.True(registry.TryRegisterAgent("owner", ScenarioId(i), (ushort)i, rider, 3));
            }

            Agent riddenHorse = mock.SpawnMount(riderAgents[1]);
            Assert.True(AgentMirror.TryGet(riddenHorse, out MirrorAgent riddenMirror));
            Assert.True(registry.TryRegisterAgent("owner", ScenarioId(7), 7, riddenHorse, 7));
            Agent horse = mock.SpawnMount();
            Assert.True(AgentMirror.TryGet(horse, out MirrorAgent horseMirror));
            Assert.True(registry.TryRegisterAgent("owner", ScenarioId(8), 8, horse));
            Agent remote = SpawnRider(mock);
            Assert.True(registry.TryRegisterAgent("other", ScenarioId(9), 9, remote));
            var moving = new List<MirrorAgent>(riders) { riddenMirror, horseMirror };

            for (int tick = 0; tick < 12; tick++)
            {
                for (int i = 0; i < moving.Count; i++)
                {
                    if ((tick + i) % 3 != 0)
                        moving[i].Position = new Vec3(tick * (i + 1), i, 0f);
                }

                // Stale local state must be dropped when an agent leaves and picked up again when it returns.
                if (tick == 4) Assert.True(registry.RemoveAgent(ScenarioId(3)));
                if (tick == 6)
                {
                    Agent late = SpawnRider(mock);
                    Assert.True(registry.TryRegisterAgent("owner", ScenarioId(10), 10, late));
                    Assert.True(AgentMirror.TryGet(late, out MirrorAgent lateMirror));
                    moving.Add(lateMirror);
                }
                if (tick == 8) Assert.True(registry.TryTransferAuthority("other", ScenarioId(5)));
                if (tick == 10) Assert.True(registry.TryTransferAuthority("owner", ScenarioId(5)));

                handler.PollMovement(tick == 0 ? 0f : 0.026f);

                network.SerializedPacketSends.Clear();
                network.DirectPacketSends.Clear();
                network.NetworkSentPackets.Packets.Clear();
                bufferStates.Add(handler.GetBufferState());
            }
        });
    }

    private static Guid ScenarioId(int number) => new Guid(number, 0, 0, new byte[8]);
}
