using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Common.Network;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Alleys;
using GameInterface.Services.CampaignService.Data;
using GameInterface.Services.Caravans;
using GameInterface.Services.Heroes;
using GameInterface.Services.Inventory;
using GameInterface.Services.Inventory.TradeSkills;
using GameInterface.Services.MobileParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Smithing;
using GameInterface.Services.Workshops;
using LiteNetLib;
using ProtoBuf;

namespace E2E.Tests.Environment;

public sealed class DedicatedServerProductionWireContractTests
{
    [Fact]
    public void ProductionSerializerAndRegistrationsMatchDedicatedServerWireContract()
    {
        var moduleRequest = new NetworkModuleVersionsValidate(Array.Empty<GameInterface.Services.Modules.ModuleInfo>(), "client-build");
        var moduleResult = new NetworkModuleVersionsValidated(false, "denied", "server-build");
        var clientRequest = new NetworkClientValidate("ds-synthetic-client-a");
        var clientResult = new NetworkClientValidated(false, null!);
        var heartbeat = new CampaignTimePacket(123456789, -1);
        var saveChunk = new GameSaveDataChunkPacket(
            7,
            0,
            1,
            3,
            5,
            new byte[] { 1, 2, 3 },
            "campaign-a",
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        ICommonSerializer sender = CreateSerializer();
        ICommonSerializer recipient = CreateSerializer();

        AssertRoundTrip(sender, recipient, heartbeat, 488231864);
        AssertRoundTrip(sender, recipient, moduleRequest, 1457133576);
        AssertRoundTrip(sender, recipient, moduleResult, 1206877260);
        AssertRoundTrip(sender, recipient, clientRequest, 791628818);
        AssertRoundTrip(sender, recipient, clientResult, 29530214);
        AssertRoundTrip(sender, recipient, saveChunk, 404232623);

        byte[] moduleWire = sender.Serialize(moduleRequest);
        byte[] clientWire = sender.Serialize(clientRequest);
        var aggregate = new AggregateMessagePacket(new[] { moduleWire, clientWire });
        byte[] aggregateWire = AssertRoundTrip(sender, recipient, aggregate, 1253361833);
        var decodedAggregate = Assert.IsType<AggregateMessagePacket>(recipient.Deserialize(aggregateWire));
        Assert.Collection(
            decodedAggregate.Messages,
            payload => Assert.IsType<NetworkModuleVersionsValidate>(recipient.Deserialize(payload)),
            payload => Assert.IsType<NetworkClientValidate>(recipient.Deserialize(payload)));

        Assert.Equal(DeliveryMethod.Sequenced, heartbeat.DeliveryMethod);
        Assert.Equal((byte)0, CoopNetworkBase.GetChannel(heartbeat));
        Assert.Equal(DeliveryMethod.ReliableOrdered, aggregate.DeliveryMethod);
        Assert.Equal((byte)0, CoopNetworkBase.GetChannel(aggregate));
        Assert.Equal(DeliveryMethod.ReliableOrdered, saveChunk.DeliveryMethod);
        Assert.Equal(CoopNetworkBase.BulkChannel, CoopNetworkBase.GetChannel(saveChunk));

        var decodedHeartbeat = Assert.IsType<CampaignTimePacket>(recipient.Deserialize(sender.Serialize(heartbeat)));
        Assert.Equal(heartbeat.ServerTicks, decodedHeartbeat.ServerTicks);
        Assert.Equal(heartbeat.JoinPacketsRemaining, decodedHeartbeat.JoinPacketsRemaining);
        var decodedSave = Assert.IsType<GameSaveDataChunkPacket>(recipient.Deserialize(sender.Serialize(saveChunk)));
        Assert.Equal(saveChunk.TransferId, decodedSave.TransferId);
        Assert.Equal(saveChunk.ChunkData, decodedSave.ChunkData);
    }

    private static byte[] AssertRoundTrip<T>(
        ICommonSerializer sender,
        ICommonSerializer recipient,
        T value,
        int expectedTypeId)
    {
        byte[] wire = sender.Serialize(value!);
        WireWrapper wrapper;
        using (var stream = new MemoryStream(wire))
        {
            wrapper = Serializer.Deserialize<WireWrapper>(stream);
        }

        Assert.Equal(expectedTypeId, wrapper.TypeId);
        Assert.NotNull(wrapper.Data);
        Assert.IsType<T>(recipient.Deserialize(wire));
        return wire;
    }

    private static ICommonSerializer CreateSerializer() =>
        new ProtoBufSerializer(new SerializableTypeMapper());

    [ProtoContract]
    private sealed class WireWrapper
    {
        [ProtoMember(1)]
        public int TypeId { get; set; }

        [ProtoMember(2)]
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
