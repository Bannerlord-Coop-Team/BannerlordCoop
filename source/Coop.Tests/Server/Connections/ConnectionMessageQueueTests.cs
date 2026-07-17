using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Tests.Extensions;
using Coop.Tests.Mocks;
using LiteNetLib;
using System;
using System.Runtime.Serialization;
using Xunit;

namespace Coop.Tests.Server.Connections;

public class ConnectionMessageQueueTests
{
    private readonly TestMessageBroker messageBroker = new();
    private readonly TestNetwork network = new();
    private readonly ConnectionMessageQueue queue;

    public ConnectionMessageQueueTests()
    {
        queue = new ConnectionMessageQueue(new Lazy<INetwork>(() => network), messageBroker);
    }

    /// <summary>Minimal broadcast packet stub.</summary>
    private sealed class FakePacket : IPacket
    {
        public PacketType PacketType => PacketType.Message;
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;
    }

    private sealed class FakeCampaignTimePacket : IPacket
    {
        public PacketType PacketType => PacketType.CampaignTime;
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Sequenced;
    }

    /// <summary>Creates a peer and drives it to the Dropping phase (connected, pre-save).</summary>
    private NetPeer Connect()
    {
        var peer = network.CreatePeer();
        messageBroker.Publish(this, new PlayerConnected(peer));
        return peer;
    }

    private bool NothingSentTo(NetPeer peer) => network.SentPayloads.ContainsKey(peer.Id) == false;

    // NetPeer equality is endpoint-based and TestNetwork.CreatePeer reuses one endpoint, so a second
    // distinct peer must be given its own endpoint to be a distinct dictionary key.
    private static int distinctPeerId = 1000;
    private static NetPeer PeerWithEndpoint(string ip)
    {
        var peer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
        peer.Setup(distinctPeerId++, ip);
        return peer;
    }

    [Fact]
    public void UntrackedPeer_PassesThroughLive()
    {
        var peer = network.CreatePeer();

        // No channel: a fully-joined or unknown peer receives broadcasts live.
        Assert.False(queue.TryHandleBroadcast(peer, new FakePacket()));
    }

    [Fact]
    public void Connected_DropsPreSaveBroadcasts_NothingReplayed()
    {
        var peer = Connect();

        // Dropping phase: the queue takes the packet (true) but discards it — it is already in the save.
        Assert.True(queue.TryHandleBroadcast(peer, new FakePacket()));

        queue.Flush(peer);

        // The dropped packet is never replayed.
        Assert.True(NothingSentTo(peer));
    }

    [Fact]
    public void Queueing_HoldsBroadcasts_ThenFlushesFifoWhileRemainingQueued()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);

        var first = new FakePacket();
        var second = new FakePacket();
        var third = new FakePacket();
        Assert.True(queue.TryHandleBroadcast(peer, first));
        Assert.True(queue.TryHandleBroadcast(peer, second));
        Assert.True(queue.TryHandleBroadcast(peer, third));

        // Held, not sent, while the peer loads.
        Assert.True(NothingSentTo(peer));

        queue.Flush(peer);

        Assert.Equal(new IPacket[] { first, second, third }, network.GetPeerPackets(peer));

        var afterFlush = new FakePacket();
        Assert.True(queue.TryHandleBroadcast(peer, afterFlush));
        Assert.DoesNotContain(afterFlush, network.GetPeerPackets(peer));
    }

    [Fact]
    public void CampaignTimeBypassesTheLoadQueue()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);

        Assert.False(queue.TryHandleBroadcast(peer, new FakeCampaignTimePacket()));

        queue.Flush(peer);
        Assert.True(NothingSentTo(peer));
    }

    [Fact]
    public void CatchUpProgress_CombinesHeldAndReliablePacketsUntilClientAcknowledges()
    {
        var peer = Connect();
        peer.SetQueueLength(5);

        Assert.False(queue.TryGetCatchUpPacketsRemaining(peer, out _));

        queue.BeginQueueing(peer);
        queue.TryHandleBroadcast(peer, new FakePacket());
        queue.TryHandleBroadcast(peer, new FakePacket());

        Assert.True(queue.TryGetCatchUpPacketsRemaining(peer, out int queued));
        Assert.Equal(7, queued);

        queue.Flush(peer);
        Assert.True(queue.TryGetCatchUpPacketsRemaining(peer, out int draining));
        Assert.Equal(5, draining);

        queue.OpenWithTail(peer, new NetworkJoinWorldReady());
        Assert.True(queue.TryGetCatchUpPacketsRemaining(peer, out int opened));
        Assert.Equal(5, opened);

        queue.CompleteCatchUp(peer);
        Assert.False(queue.TryGetCatchUpPacketsRemaining(peer, out _));
    }

    [Fact]
    public void BeginQueueing_BeforePlayerConnected_StillQueuesAndFlushes()
    {
        var peer = network.CreatePeer();

        // BeginQueueing (main save thread) can race ahead of the PlayerConnected handler (poll loop);
        // GetOrAdd creates the channel directly in Queueing so broadcasts are held, not sent live.
        queue.BeginQueueing(peer);

        var held = new FakePacket();
        Assert.True(queue.TryHandleBroadcast(peer, held));
        Assert.True(NothingSentTo(peer));

        queue.Flush(peer);
        Assert.Equal(new IPacket[] { held }, network.GetPeerPackets(peer));
    }

    [Fact]
    public void Flush_KeepsPeerQueuedUntilOpenWithTail()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);
        queue.Flush(peer);

        var held = new FakePacket();
        Assert.True(queue.TryHandleBroadcast(peer, held));
        Assert.True(NothingSentTo(peer));

        var marker = new NetworkJoinWorldReady();
        queue.OpenWithTail(peer, marker);

        Assert.Equal(new object[] { held, marker }, network.GetPeerPayloads(peer));
        Assert.False(queue.TryHandleBroadcast(peer, new FakePacket()));
    }

    [Fact]
    public void OpenWithTail_SendsHeldPacketsThenMarkerThenLivePackets()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);

        var beforeFlush = new FakePacket();
        queue.TryHandleBroadcast(peer, beforeFlush);
        queue.Flush(peer);

        var afterFlush = new FakePacket();
        queue.TryHandleBroadcast(peer, afterFlush);

        var marker = new NetworkJoinWorldReady();
        queue.OpenWithTail(peer, marker);

        var live = new FakePacket();
        Assert.False(queue.TryHandleBroadcast(peer, live));
        network.Send(peer, live);

        Assert.Equal(
            new object[] { beforeFlush, afterFlush, marker, live },
            network.GetPeerPayloads(peer));
    }

    [Fact]
    public void DisconnectMidLoad_DropsHeldPackets_AndPeerGoesLive()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);
        queue.TryHandleBroadcast(peer, new FakePacket());

        messageBroker.Publish(this, new PlayerDisconnected(peer, default));

        // Untracked again -> live; a late flush finds no channel and replays nothing.
        Assert.False(queue.TryHandleBroadcast(peer, new FakePacket()));
        queue.Flush(peer);
        Assert.True(NothingSentTo(peer));
    }

    [Fact]
    public void RemovalIsIdempotent()
    {
        var peer = network.CreatePeer();

        // Disconnect for a peer that never connected, then connect and double-disconnect: no throw.
        messageBroker.Publish(this, new PlayerDisconnected(peer, default));
        messageBroker.Publish(this, new PlayerConnected(peer));
        messageBroker.Publish(this, new PlayerDisconnected(peer, default));
        messageBroker.Publish(this, new PlayerDisconnected(peer, default));
    }

    [Fact]
    public void NetworkCampaignEntered_DoesNotFlush()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);
        queue.TryHandleBroadcast(peer, new FakePacket());

        // Mission<->campaign transitions republish the NETWORK message; it must never flush the queue.
        messageBroker.Publish(this, new NetworkPlayerCampaignEntered());

        Assert.True(NothingSentTo(peer));
    }

    [Fact]
    public void PerPeerIndependence_OneHeldOneLive()
    {
        var loading = Connect();
        queue.BeginQueueing(loading);

        var joined = PeerWithEndpoint("127.0.0.2"); // distinct endpoint, never tracked -> live

        Assert.True(queue.TryHandleBroadcast(loading, new FakePacket()));  // held
        Assert.False(queue.TryHandleBroadcast(joined, new FakePacket()));  // live
    }
}
