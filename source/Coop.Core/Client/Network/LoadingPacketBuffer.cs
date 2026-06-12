using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Coop.Core.Client.Network;

/// <summary>
/// Buffers gameplay packets that arrive at a joining client while it is loading the transfer save,
/// then replays them in order once the campaign is ready.
/// </summary>
/// <remarks>
/// Without this, world-change packets (chiefly DynamicSync messages) are handled against a campaign
/// that has not loaded yet and are lost or throw. This generalises the per-message deferral that
/// <c>RemotePlayerHeroHandler</c> does for new-player heroes.
///
/// Lifecycle, driven by the save's arrival and the client state machine:
/// <list type="bullet">
/// <item>Off by default — packets pass straight through (normal play and the pre-save handshake).</item>
/// <item>The <see cref="PacketType.SaveData"/> packet arms buffering and then passes through (it
/// starts the load). Everything after it on the ordered channel is a post-snapshot delta.</item>
/// <item>While armed, every packet except the save and <see cref="PacketType.PacketWrapper"/> is
/// queued. This is deadlock-safe: the client's load is driven by local game-state events and needs
/// no further incoming packet to finish.</item>
/// <item>On <see cref="ClientCampaignEntered"/> the queue is drained in FIFO order and buffering
/// stops. <see cref="MainMenuEntered"/> (disconnect/abort) clears the queue and resets.</item>
/// </list>
/// Threading: <see cref="Intercept"/> and <see cref="DrainIfRequested"/> are both called on the
/// network poller thread (CoopClient receive + update), so the queue is effectively single-threaded;
/// only the drain/reset requests are raised from the broker thread and are therefore volatile flags.
/// </remarks>
public interface ILoadingPacketBuffer
{
    /// <summary>
    /// Returns true if the packet was buffered and must NOT be handled now; false if the caller
    /// should handle it immediately.
    /// </summary>
    bool Intercept(NetPeer peer, IPacket packet);

    /// <summary>
    /// If the campaign just became ready, stops buffering and returns the buffered packets in FIFO
    /// order for replay; otherwise returns an empty list.
    /// </summary>
    IReadOnlyList<(NetPeer Peer, IPacket Packet)> DrainIfRequested();
}

internal sealed class LoadingPacketBuffer : ILoadingPacketBuffer, IDisposable
{
    private readonly IMessageBroker messageBroker;
    private readonly ConcurrentQueue<(NetPeer, IPacket)> queue = new();

    private volatile bool buffering;
    private volatile bool drainRequested;

    public LoadingPacketBuffer(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<ClientCampaignEntered>(Handle_ClientCampaignEntered);
        messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClientCampaignEntered>(Handle_ClientCampaignEntered);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
    }

    public bool Intercept(NetPeer peer, IPacket packet)
    {
        // The save arms buffering, but is itself always handled — it drives the load that ends it.
        if (packet.PacketType == PacketType.SaveData)
        {
            buffering = true;
            return false;
        }

        if (!buffering) return false;

        // Infrastructure wrapper is never gameplay state — let it through.
        if (packet.PacketType == PacketType.PacketWrapper) return false;

        queue.Enqueue((peer, packet));
        return true;
    }

    public IReadOnlyList<(NetPeer Peer, IPacket Packet)> DrainIfRequested()
    {
        if (!drainRequested) return Array.Empty<(NetPeer, IPacket)>();

        drainRequested = false;
        buffering = false;

        var drained = new List<(NetPeer, IPacket)>();
        while (queue.TryDequeue(out var item))
        {
            drained.Add(item);
        }
        return drained;
    }

    private void Handle_ClientCampaignEntered(MessagePayload<ClientCampaignEntered> payload)
    {
        // Defer the drain to the poller thread (DrainIfRequested) so replayed packets stay ordered
        // relative to live ones and the queue stays single-threaded.
        drainRequested = true;
    }

    private void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> payload)
    {
        // Left the campaign (disconnect/abort): drop the backlog and disarm so a reconnect starts clean.
        buffering = false;
        drainRequested = false;
        while (queue.TryDequeue(out _)) { }
    }
}
