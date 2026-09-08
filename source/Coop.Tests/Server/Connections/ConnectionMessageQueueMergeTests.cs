using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.ItemRosters.Messages;
using Coop.Tests.Mocks;
using GameInterface.Registry.Auto;
using GameInterface.Services.Workshops.Messages;
using LiteNetLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace Coop.Tests.Server.Connections;

/// <summary>Checks safe adjacent merging, replay barriers and queue admission bounds.</summary>
public class ConnectionMessageQueueMergeTests
{
    private readonly TestNetwork network = new();
    private readonly TestMessageBroker broker = new();
    private readonly ICommonSerializer serializer = new ProtoBufSerializer(new SerializableTypeMapper());

    private ConnectionMessageQueue Queue(int packets = 100000, long bytes = 128 * 1024 * 1024) =>
        new(new Lazy<INetwork>(() => network), broker, serializer, packets, bytes);

    private NetPeer Join(ConnectionMessageQueue queue)
    {
        var peer = network.CreatePeer();
        queue.RegisterPeer(peer);
        queue.BeginQueueing(peer);
        return peer;
    }

    private MessagePacket Item(int amount, string roster = "roster", string item = "item", string? modifier = null) =>
        MessagePacket.Create(new NetworkItemRosterUpdate(roster, item, modifier!, amount), serializer);

    private void Drain(ConnectionMessageQueue queue, NetPeer peer)
    {
        while (queue.FlushBatch(peer).HasMore) { }
    }

    [Fact]
    public void AdjacentItems_ReduceCountAndBytesWithoutMutatingSharedPackets()
    {
        using var queue = Queue();
        var firstPeer = Join(queue);
        var secondPeer = Join(queue);
        var first = Item(1);
        var next = Item(2);
        queue.TryHandleBroadcast(firstPeer, first);
        queue.TryHandleBroadcast(secondPeer, first);
        queue.TryHandleBroadcast(firstPeer, next);

        Assert.True(queue.TryGetCatchUpPacketsRemaining(firstPeer, out int count));
        Assert.Equal(1, count);
        Assert.True(queue.TryGetCatchUpPendingBytes(firstPeer, out long bytes));
        Assert.Equal(Item(3).Data.Length, bytes);
        Assert.Equal(1, serializer.Deserialize<NetworkItemRosterUpdate>(first.Data).Amount);
        Assert.Contains("mergedAdjacent=1", queue.DescribeCatchUp(firstPeer));

        Drain(queue, firstPeer);
        Drain(queue, secondPeer);
        Assert.Equal(3, ReadItems(firstPeer).Single().Amount);
        Assert.Equal(1, ReadItems(secondPeer).Single().Amount);
        Assert.True(queue.TryGetCatchUpPendingBytes(firstPeer, out bytes));
        Assert.Equal(0, bytes);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(-10, -10)]
    [InlineData(1, -1)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MinValue, -1)]
    public void RemovalsSignChangesZeroAndIntegerOverflow_RemainSeparate(int first, int next)
    {
        using var queue = Queue();
        var peer = Join(queue);
        queue.TryHandleBroadcast(peer, Item(first));
        queue.TryHandleBroadcast(peer, Item(next));
        Drain(queue, peer);
        Assert.Equal(new[] { first, next }, ReadItems(peer).Select(item => item.Amount));
    }

    [Fact]
    public void ClearLifetimeWorkshopAndIdentityChanges_AreOrderedBarriers()
    {
        using var queue = Queue();
        var peer = Join(queue);
        var packets = new[]
        {
            Item(1), Item(2, modifier: "fine"), Item(3, item: "food"), Item(4, roster: "other"),
            Item(5), MessagePacket.Create(new NetworkItemRosterClear("roster"), serializer), Item(6),
            MessagePacket.Create(new NetworkDestroyInstance<ItemRoster>("roster"), serializer),
            MessagePacket.Create(new NetworkCreateInstance<ItemRoster>("roster"), serializer), Item(7),
            MessagePacket.Create(new AddOutputProgressForTown("workshop", 0.1f), serializer), Item(8)
        };
        foreach (var packet in packets) queue.TryHandleBroadcast(peer, packet);
        Drain(queue, peer);
        Assert.Equal(packets.Select(packet => packet.Data),
            network.GetPeerPackets(peer).Cast<MessagePacket>().Select(packet => packet.Data));
    }

    [Fact]
    public void FinalBaselineAndDrainedCandidates_DoNotCombineWithLaterTraffic()
    {
        using var queue = Queue();
        var peer = Join(queue);
        queue.TryHandleBroadcast(peer, Item(1));
        Drain(queue, peer);
        queue.TryHandleBroadcast(peer, Item(2));
        queue.EndFinalBaselineCoverage(peer);
        queue.TryHandleBroadcast(peer, Item(3));
        queue.TryHandleBroadcast(peer, Item(4));
        var tail = new NetworkItemRosterClear("tail");
        while (queue.OpenWithTailBatch(peer, tail, () => true).HasMore) { }

        Assert.Equal(new[] { 1, 2, 3, 4 }, ReadItems(peer).Select(item => item.Amount));
        Assert.Equal(tail, network.GetPeerPayloads(peer).Last());
        Assert.False(queue.TryHandleBroadcast(peer, Item(5)));
    }


    [Fact]
    public void AbortDisconnectAndSnapshotCuts_DiscardMergeCandidates()
    {
        using var queue = Queue();
        var peer = Join(queue);
        queue.TryHandleBroadcast(peer, Item(1));
        queue.TryHandleBroadcast(peer, Item(2));
        Assert.Equal(1, queue.AbortCatchUp(peer));
        queue.TryHandleBroadcast(peer, Item(100));
        queue.BeginQueueing(peer);
        queue.TryHandleBroadcast(peer, Item(4));
        queue.BeginQueueing(peer);
        queue.TryHandleBroadcast(peer, Item(5));
        Drain(queue, peer);
        Assert.Equal(new[] { 4, 5 }, ReadItems(peer).Select(item => item.Amount));

        queue.TryHandleBroadcast(peer, Item(6));
        broker.Publish(this, new PlayerDisconnected(peer, default));
        queue.RegisterPeer(peer);
        queue.TryHandleBroadcast(peer, Item(100));
        queue.BeginQueueing(peer);
        queue.TryHandleBroadcast(peer, Item(7));
        Drain(queue, peer);
        Assert.Equal(new[] { 4, 5, 7 }, ReadItems(peer).Select(item => item.Amount));
    }

    [Fact]
    public void MergingAtPacketLimit_StillHonorsByteOverflowAndRetainsValidPrefix()
    {
        var first = Item(1);
        using var queue = Queue(packets: 1, bytes: first.Data.Length);
        var peer = Join(queue);
        queue.TryHandleBroadcast(peer, first);
        queue.TryHandleBroadcast(peer, Item(1));
        Assert.False(queue.HasCatchUpOverflowed(peer));
        queue.TryHandleBroadcast(peer, Item(int.MaxValue - 2));
        Assert.True(queue.HasCatchUpOverflowed(peer));
        queue.TryHandleBroadcast(peer, Item(1));
        var result = queue.OpenWithTailBatch(peer, new NetworkItemRosterClear("tail"), () => true);
        Assert.True(result.Overflowed);
        Assert.Equal(2, ReadItems(peer).Single().Amount);
        Assert.DoesNotContain(network.ImmediateSends, send => send.Payload is IMessage);
    }

    private NetworkItemRosterUpdate[] ReadItems(NetPeer peer) =>
        network.GetPeerPackets(peer).Cast<MessagePacket>()
            .Where(packet => packet.MessageType == typeof(NetworkItemRosterUpdate))
            .Select(packet => serializer.Deserialize<NetworkItemRosterUpdate>(packet.Data)).ToArray();
}
