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

    /// <summary>Creates a peer and drives it to the Dropping phase (connected, pre-save).</summary>
    private NetPeer Connect()
    {
        var peer = network.CreatePeer();
        messageBroker.Publish(this, new PlayerConnected(peer));
        return peer;
    }

    private bool NothingSentTo(NetPeer peer) => network.SentPackets.ContainsKey(peer.Id) == false;

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

        messageBroker.Publish(this, new PlayerCampaignEntered(peer));

        // The dropped packet is never replayed.
        Assert.True(NothingSentTo(peer));
    }

    [Fact]
    public void Queueing_HoldsBroadcasts_ThenFlushesFifoOnCampaignEntered()
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

        messageBroker.Publish(this, new PlayerCampaignEntered(peer));

        Assert.Equal(new IPacket[] { first, second, third }, network.GetPeerPackets(peer));
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

        messageBroker.Publish(this, new PlayerCampaignEntered(peer));
        Assert.Equal(new IPacket[] { held }, network.GetPeerPackets(peer));
    }

    [Fact]
    public void AfterCampaignEntered_PeerReceivesLive()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);
        messageBroker.Publish(this, new PlayerCampaignEntered(peer));

        Assert.False(queue.TryHandleBroadcast(peer, new FakePacket()));
    }

    [Fact]
    public void ReplayedPacketsPrecedeLivePackets()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);

        var held = new FakePacket();
        queue.TryHandleBroadcast(peer, held);

        messageBroker.Publish(this, new PlayerCampaignEntered(peer)); // flush replays held

        // A broadcast after the flush is live; the caller sends it, landing strictly after the replay.
        var live = new FakePacket();
        Assert.False(queue.TryHandleBroadcast(peer, live));
        network.Send(peer, live);

        Assert.Equal(new IPacket[] { held, live }, network.GetPeerPackets(peer));
    }

    [Fact]
    public void DisconnectMidLoad_DropsHeldPackets_AndPeerGoesLive()
    {
        var peer = Connect();
        queue.BeginQueueing(peer);
        queue.TryHandleBroadcast(peer, new FakePacket());

        messageBroker.Publish(this, new PlayerDisconnected(peer, default));

        // Untracked again -> live; a late campaign-entered finds no channel and replays nothing.
        Assert.False(queue.TryHandleBroadcast(peer, new FakePacket()));
        messageBroker.Publish(this, new PlayerCampaignEntered(peer));
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
    public void NetworkCampaignEntered_DoesNotFlush_OnlyLocalDoes()
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
