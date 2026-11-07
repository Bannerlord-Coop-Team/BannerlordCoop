using System.Security.Cryptography;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
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
    // SHA-256 prefix of every datagram the scenario below sends, recorded from the allocate-per-tick code on development.
    private static readonly string[] BufferReuseScenarioDatagrams = Array.Empty<string>();

    // Two runs, because a datagram that differs between runs of the same scenario is not a usable reference.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void PollMovement_ScenarioDatagramsMatchTheAllocatePerTickBytes(int run)
    {
        string[] datagrams = RunBufferReuseScenario();

        output.WriteLine($"run {run}");
        foreach (string line in datagrams)
            output.WriteLine($"DATAGRAM {line}");
        Assert.Equal(BufferReuseScenarioDatagrams, datagrams);
    }

    private string[] RunBufferReuseScenario()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner");
        string[] datagrams = Array.Empty<string>();

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            foreach (string receiver in new[] { "r1", "r2", "r3" })
                broker.Publish(this, new NetworkMissionPeerEntered(receiver, "battle"));

            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            component.AgentMovementHandler.Dispose();
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
            using var handler = new AgentMovementHandler(
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

            var lines = new List<string>();
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

                foreach (SerializedPacketSend send in network.SerializedPacketSends)
                {
                    byte[] hash = SHA256.HashData(send.Payload);
                    lines.Add(
                        $"{tick}|{send.ControllerId}|{send.Packet.GetType().Name}|{send.Payload.Length}|" +
                        Convert.ToHexString(hash, 0, 8));
                }
                network.SerializedPacketSends.Clear();
                network.DirectPacketSends.Clear();
                network.NetworkSentPackets.Packets.Clear();
            }

            datagrams = lines.ToArray();
        });

        return datagrams;
    }

    private static Guid ScenarioId(int number) => new Guid(number, 0, 0, new byte[8]);
}
