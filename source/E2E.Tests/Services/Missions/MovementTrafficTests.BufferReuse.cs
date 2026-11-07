using System.Security.Cryptography;
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
    // SHA-256 prefix of every datagram the scenario below sends, recorded from the allocate-per-tick code on development.
    private static readonly string[] BufferReuseScenarioDatagrams =
    {
        "0|r1|MovementPacket|38|F47486E887B40468",
        "0|r1|MovementPacket|90|3C4A8087CC7BE2CC",
        "0|r1|MovementPacket|47|6E0ECA159F9C7DEF",
        "0|r1|MovementPacket|38|F605A3448A7CAB0C",
        "0|r1|MovementPacket|47|A38EE7F5FC852D0A",
        "0|r1|MovementPacket|47|96A93E9DD0E47174",
        "0|r1|MountMovementPacket|75|353B9FC843F49A95",
        "0|r2|MovementPacket|38|F47486E887B40468",
        "0|r2|MovementPacket|90|3C4A8087CC7BE2CC",
        "0|r2|MovementPacket|47|6E0ECA159F9C7DEF",
        "0|r2|MovementPacket|38|F605A3448A7CAB0C",
        "0|r2|MovementPacket|47|A38EE7F5FC852D0A",
        "0|r2|MovementPacket|47|96A93E9DD0E47174",
        "0|r2|MountMovementPacket|75|353B9FC843F49A95",
        "0|r3|MovementPacket|38|F47486E887B40468",
        "0|r3|MovementPacket|90|3C4A8087CC7BE2CC",
        "0|r3|MovementPacket|47|6E0ECA159F9C7DEF",
        "0|r3|MovementPacket|38|F605A3448A7CAB0C",
        "0|r3|MovementPacket|47|A38EE7F5FC852D0A",
        "0|r3|MovementPacket|47|96A93E9DD0E47174",
        "0|r3|MountMovementPacket|75|353B9FC843F49A95",
        "1|r1|MovementPacket|47|C36174A44A813AAD",
        "1|r1|MovementPacket|99|EB015BB68603AECD",
        "1|r1|MovementPacket|47|EE1BC74403CCA235",
        "1|r1|MovementPacket|47|ED537237C06A3EAC",
        "1|r1|MountMovementPacket|75|E2564F5CC32C86DC",
        "1|r2|MovementPacket|47|C36174A44A813AAD",
        "1|r2|MovementPacket|99|EB015BB68603AECD",
        "1|r2|MovementPacket|47|EE1BC74403CCA235",
        "1|r2|MovementPacket|47|ED537237C06A3EAC",
        "1|r2|MountMovementPacket|75|E2564F5CC32C86DC",
        "1|r3|MovementPacket|47|C36174A44A813AAD",
        "2|r3|MovementPacket|47|F88ABA79D112C3F7",
        "2|r3|MovementPacket|99|7EED4D6B7A406F31",
        "2|r3|MovementPacket|47|21CBAFBC39F4A906",
        "2|r3|MovementPacket|47|DEDF448CC46E8C3E",
        "2|r3|MovementPacket|47|BEFA38B05347AE97",
        "2|r3|MovementPacket|47|A1C75873E7A2658B",
        "2|r3|MountMovementPacket|75|A1C213A4F393BD36",
        "2|r1|MovementPacket|47|F88ABA79D112C3F7",
        "2|r1|MovementPacket|47|21CBAFBC39F4A906",
        "2|r1|MovementPacket|47|A1C75873E7A2658B",
        "2|r1|MovementPacket|99|7EED4D6B7A406F31",
        "2|r1|MovementPacket|47|DEDF448CC46E8C3E",
        "2|r2|MovementPacket|47|F88ABA79D112C3F7",
        "2|r2|MovementPacket|47|21CBAFBC39F4A906",
        "2|r2|MovementPacket|47|A1C75873E7A2658B",
        "2|r2|MovementPacket|99|7EED4D6B7A406F31",
        "2|r2|MovementPacket|47|DEDF448CC46E8C3E",
        "3|r1|MovementPacket|47|D924C8EAD977902D",
        "3|r1|MountMovementPacket|75|428327B2FAB1404A",
        "3|r1|MovementPacket|99|35B3DD09E9014BDF",
        "3|r1|MovementPacket|47|0F30F2EE82B21A5E",
        "3|r1|MovementPacket|47|A73FAD9764510A85",
        "3|r2|MovementPacket|47|D924C8EAD977902D",
        "3|r2|MountMovementPacket|75|428327B2FAB1404A",
        "3|r2|MovementPacket|99|35B3DD09E9014BDF",
        "3|r2|MovementPacket|47|0F30F2EE82B21A5E",
        "3|r2|MovementPacket|47|A73FAD9764510A85",
        "4|r3|MovementPacket|47|F7A26CA132A5D0F2",
        "4|r3|MovementPacket|99|D7E3B7001E36E036",
        "4|r3|MovementPacket|47|7F54902AA7779731",
        "4|r3|MovementPacket|47|519815C42457F3E7",
        "4|r3|MovementPacket|47|E79F1D98673CBDEC",
        "4|r3|MountMovementPacket|75|255A09D3A3E31658",
        "4|r1|MovementPacket|47|F7A26CA132A5D0F2",
        "4|r1|MovementPacket|47|7F54902AA7779731",
        "4|r1|MovementPacket|99|D7E3B7001E36E036",
        "4|r1|MovementPacket|47|519815C42457F3E7",
        "4|r1|MountMovementPacket|75|255A09D3A3E31658",
        "4|r2|MovementPacket|47|F7A26CA132A5D0F2",
        "4|r2|MovementPacket|47|7F54902AA7779731",
        "4|r2|MovementPacket|99|D7E3B7001E36E036",
        "4|r2|MovementPacket|47|519815C42457F3E7",
        "4|r2|MountMovementPacket|75|255A09D3A3E31658",
        "5|r3|MovementPacket|47|549CF13F02555715",
        "5|r1|MovementPacket|47|549CF13F02555715",
        "5|r1|MovementPacket|47|C286A71DB94CBD60",
        "5|r1|MovementPacket|99|ADA6D4B494568F21",
        "5|r1|MovementPacket|47|E18C3CE47F381CDA",
        "5|r2|MovementPacket|47|549CF13F02555715",
        "5|r2|MovementPacket|47|C286A71DB94CBD60",
        "5|r2|MovementPacket|99|ADA6D4B494568F21",
        "5|r2|MovementPacket|47|E18C3CE47F381CDA",
        "6|r3|MovementPacket|151|E7C955CC1A2A0B4A",
        "6|r3|MountMovementPacket|75|ECDF9453E8AFB82F",
        "6|r1|MovementPacket|61|F4B5E8540FF36EB7",
        "6|r1|MountMovementPacket|75|ECDF9453E8AFB82F",
        "6|r1|MovementPacket|116|B6501650207455E9",
        "6|r2|MovementPacket|61|F4B5E8540FF36EB7",
        "6|r2|MountMovementPacket|75|ECDF9453E8AFB82F",
        "6|r2|MovementPacket|116|B6501650207455E9",
        "7|r3|MovementPacket|47|ACA0DE68DAEA10A6",
        "7|r1|MovementPacket|47|ACA0DE68DAEA10A6",
        "7|r1|MovementPacket|116|1BDA144CBCEA50DD",
        "7|r1|MovementPacket|47|1A8C39A077A052A8",
        "7|r1|MountMovementPacket|75|981F0A8B216D6B63",
        "7|r2|MovementPacket|47|ACA0DE68DAEA10A6",
        "7|r2|MovementPacket|116|1BDA144CBCEA50DD",
        "7|r2|MovementPacket|47|1A8C39A077A052A8",
        "7|r2|MountMovementPacket|75|981F0A8B216D6B63",
        "8|r3|MovementPacket|47|6302A2290C7A03DE",
        "8|r3|MovementPacket|130|4ACFC7F855176FAC",
        "8|r3|MountMovementPacket|75|3F0E0B55A234B417",
        "8|r3|MovementPacket|47|37224BB90226054E",
        "8|r1|MovementPacket|47|6302A2290C7A03DE",
        "8|r1|MovementPacket|70|6867D2E59E505681",
        "8|r1|MovementPacket|115|4A2EDBF7D00DFA04",
        "8|r2|MovementPacket|47|6302A2290C7A03DE",
        "8|r2|MovementPacket|70|6867D2E59E505681",
        "8|r2|MovementPacket|115|4A2EDBF7D00DFA04",
        "9|r1|MountMovementPacket|75|0437889D9C697002",
        "9|r1|MovementPacket|132|3DBA90D482B28E3C",
        "9|r2|MountMovementPacket|75|0437889D9C697002",
        "9|r2|MovementPacket|132|3DBA90D482B28E3C",
        "10|r3|MovementPacket|47|8ED17B911E0C63F3",
        "10|r3|MovementPacket|146|60551F9E0E133D52",
        "10|r3|MountMovementPacket|75|0B3C87F19869FF60",
        "10|r3|MovementPacket|47|CF52E13FEC80E091",
        "10|r1|MovementPacket|47|8ED17B911E0C63F3",
        "10|r1|MovementPacket|130|274DAAC0B6F79EED",
        "10|r1|MountMovementPacket|75|0B3C87F19869FF60",
        "10|r2|MovementPacket|47|8ED17B911E0C63F3",
        "10|r2|MovementPacket|130|274DAAC0B6F79EED",
        "10|r2|MountMovementPacket|75|0B3C87F19869FF60",
        "11|r3|MovementPacket|47|2B6C7487633B6116",
        "11|r1|MovementPacket|47|2B6C7487633B6116",
        "11|r1|MovementPacket|131|84CBDF446CCDA9D0",
        "11|r1|MovementPacket|47|511094947D11BAFE",
        "11|r2|MovementPacket|47|2B6C7487633B6116",
        "11|r2|MovementPacket|131|84CBDF446CCDA9D0",
        "11|r2|MovementPacket|47|511094947D11BAFE</StdOut>",
    };

    // Two runs, because a datagram that differs between runs of the same scenario is not a usable reference.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void PollMovement_ScenarioDatagramsMatchTheAllocatePerTickBytes(int run)
    {
        string[] datagrams = RunBufferReuseScenario(null);

        output.WriteLine($"run {run}");
        foreach (string line in datagrams)
            output.WriteLine($"DATAGRAM {line}");
        Assert.Equal(BufferReuseScenarioDatagrams, datagrams);
    }

    [Fact]
    public void PollMovement_KeepsBufferCapacitiesAndLeavesNothingBehindBetweenTicks()
    {
        var states = new List<(int OwnedAgents, int Captured, int RecipientIds, int Retained)>();

        RunBufferReuseScenario(states);

        Assert.Equal(12, states.Count);
        Assert.All(states, state => Assert.Equal(0, state.Retained));
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
    // an agent removed and another registered, and an authority transfer out and back. Returns one line per datagram.
    private string[] RunBufferReuseScenario(
        List<(int OwnedAgents, int Captured, int RecipientIds, int Retained)>? bufferStates)
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
                bufferStates?.Add(handler.GetBufferState());
            }

            datagrams = lines.ToArray();
        });

        return datagrams;
    }

    private static Guid ScenarioId(int number) => new Guid(number, 0, 0, new byte[8]);
}
