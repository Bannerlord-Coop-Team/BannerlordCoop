using System.Security.Cryptography;
using System.Text;
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

// #4487: the per-recipient priority order must send the same datagrams as the struct sort it replaced.
public partial class MovementTrafficTests
{
    // SHA-256 of every datagram these scenarios sent before #4487 changed the priority sort (recorded on development 7d6b7a030).
    private const string RoomyPriorityOrderDatagrams = "F8CAEF6404C7A800D9EA061739D48B075636D022DDC2BC72C301A9EC900A5800";
    private const string TightPriorityOrderDatagrams = "BBBEEC1F2985E8E0599A9BA4204A76107AEE2EE485B6A3E3562D15E9B9D14B42";

    [Theory]
    [InlineData(false, RoomyPriorityOrderDatagrams)]
    [InlineData(true, TightPriorityOrderDatagrams)]
    public void PriorityOrder_RecipientsReceiveTheRecordedDatagrams(bool tightBudget, string expectedHash)
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "owner");
        string[] recipients = { "first", "second", "third" };
        string actualHash = null!;
        int datagrams = 0;

        peer.Call(() =>
        {
            var mock = fixture.CreateMission(peer);
            var broker = peer.Resolve<IMessageBroker>();
            foreach (string recipient in recipients)
                broker.Publish(this, new NetworkMissionPeerEntered(recipient, "battle"));

            var registry = peer.Resolve<INetworkAgentRegistry>();
            var component = peer.Resolve<ICoopMissionComponent>();
            component.AgentMovementHandler.Dispose();
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());

            var agents = new List<(Agent Agent, MirrorAgent Mirror, Guid Id)>();
            for (int i = 0; i < 60; i++)
            {
                Agent rider = SpawnRider(mock);
                if (i % 4 == 0) mock.SpawnMount(rider);
                Assert.True(AgentMirror.TryGet(rider, out MirrorAgent mirror));
                PopulateIncompressibleMovementState(mirror, i + 1);
                mirror.Position = new Vec3((i % 10) * 9f, (i / 10) * 11f, 0f);
                Guid id = EquivalenceGuid(i);
                Assert.True(registry.TryRegisterAgent("owner", id, (ushort)(i + 1), rider));
                agents.Add((rider, mirror, id));
            }

            var rateController = new Mock<IMovementRateController>();
            rateController
                .Setup(value => value.AdvanceFrame(It.IsAny<float>()))
                .Returns(new MovementCadence(40, 40));
            rateController
                .Setup(value => value.GetReceiverCapHz(It.IsAny<string>()))
                .Returns(40);
            rateController
                .Setup(value => value.GetReceiverIncomingBytesPerSecond(It.IsAny<string>()))
                .Returns(tightBudget ? 60_000d : 1024 * 1024d);
            // Each recipient watches a different agent, so each gets its own distance order.
            for (int i = 0; i < recipients.Length; i++)
            {
                string recipient = recipients[i];
                Guid focus = agents[(i * 23) + 5].Id;
                rateController
                    .Setup(value => value.TryGetReceiverFocusAgentId(recipient, out focus))
                    .Returns(true);
            }

            var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
            var priorityScheduler = new MovementPriorityScheduler();
            int bytesPerSecond = tightBudget ? 60_000 : 1024 * 1024;
            int burstBytes = tightBudget ? 3_000 : 1024 * 1024;
            var sender = new MovementBatchSender(
                network,
                new MovementPacketCompressor(serializer),
                new DelegateMovementTrafficBudgetFactory(() => new MovementTrafficBudget(bytesPerSecond, burstBytes)),
                priorityScheduler,
                new MovementNetworkSettings(
                    MovementNetworkSettings.DefaultMiBPerSecond,
                    MovementNetworkSettings.DefaultMiBPerSecond));
            using var handler = new AgentMovementHandler(
                network,
                peer.Resolve<IPacketManager>(),
                broker,
                registry,
                peer.Resolve<IControllerIdProvider>(),
                peer.Resolve<IAgentEquipmentApplier>(),
                sender,
                peer.Resolve<IPuppetMountStateRepairer>(),
                peer.Resolve<IAgentVisualActionAccessor>(),
                rateController.Object,
                priorityScheduler,
                peer.Resolve<IMissionContext>(),
                peer.Resolve<INavalDeckMovementProbe>(),
                peer.Resolve<ILocationNpcAuthorityState>());

            handler.PollMovement(0f);
            for (int frame = 1; frame <= 8; frame++)
            {
                // Move a different subset each frame, so send times, staleness and pending ages diverge.
                for (int i = 0; i < agents.Count; i++)
                {
                    if ((i + frame) % 3 != 0) continue;
                    MirrorAgent mirror = agents[i].Mirror;
                    mirror.Position = mirror.Position + new Vec3(0.5f * frame, -0.25f * (i % 5), 0f);
                }
                handler.PollMovement(0.025f);
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (SerializedPacketSend send in network.SerializedPacketSends)
            {
                hash.AppendData(Encoding.UTF8.GetBytes(send.ControllerId + "\n"));
                hash.AppendData(BitConverter.GetBytes(send.Payload.Length));
                hash.AppendData(send.Payload);
            }
            actualHash = Convert.ToHexString(hash.GetHashAndReset());
            datagrams = network.SerializedPacketSends.Count;
        });

        output.WriteLine($"{datagrams} datagrams, hash {actualHash}");
        Assert.True(datagrams > 9, $"Expected datagrams on most frames, got {datagrams}");
        Assert.Equal(expectedHash, actualHash);
    }
}
