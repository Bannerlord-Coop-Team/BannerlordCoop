using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.UI.PlayerList;
using LiteNetLib;
using System;
using System.Diagnostics;
using System.Linq;

namespace Coop.Core.Server.Services.Players.Handlers;

/// <summary>Replicates a presentation snapshot of the existing player registry.</summary>
internal sealed class PlayerListServerHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IPlayerManager players;
    private readonly IPlayerActivityReader activity;
    private readonly Stopwatch refresh = Stopwatch.StartNew();
    private PlayerListEntry[] last = Array.Empty<PlayerListEntry>();
    private bool disposed;

    // Subscribes to campaign frames, join completion and platform-name reports.
    public PlayerListServerHandler(IMessageBroker broker, INetwork network,
        IPlayerManager players, IPlayerActivityReader activity)
    {
        this.broker = broker;
        this.network = network;
        this.players = players;
        this.activity = activity;
        broker.Subscribe<CampaignTick>(Tick);
        broker.Subscribe<PlayerCampaignSynchronized>(Joined);
        broker.Subscribe<NetworkPlayerPlatformName>(SetName);
    }

    // Samples paused campaign frames too, without sending unchanged snapshots.
    private void Tick(MessagePayload<CampaignTick> payload)
    {
        if (disposed || refresh.ElapsedMilliseconds < 1000) return;
        refresh.Restart();
        PublishChanges();
    }

    // Sends a full baseline even if the roster has not changed since the last broadcast.
    private void Joined(MessagePayload<PlayerCampaignSynchronized> payload)
    {
        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() =>
        {
            if (disposed) return;
            // Do not take an extra movement sample between regular campaign-frame samples.
            network.Send(peer, new NetworkPlayerList(last));
        });
    }

    // Associates metadata with the registered sender, not a client-supplied controller id.
    private void SetName(MessagePayload<NetworkPlayerPlatformName> payload)
    {
        if (payload.Who is not NetPeer peer) return;
        GameThread.RunSafe(() =>
        {
            if (disposed || !players.TryGetPlayer(peer, out var player)) return;
            player.PlatformName = payload.What.Name ?? string.Empty;
        });
    }

    // Reads authoritative membership and game objects on the game thread.
    internal PlayerListEntry[] Capture() => players.Players
        .Where(player => player.ControllerId != CoopServer.ServerControllerId)
        .OrderBy(player => player.ControllerId, StringComparer.Ordinal)
        .Select(player => activity.Read(player, players.IsConnected(player))).ToArray();

    // Sends only changed presentation data; the registry remains the source of truth.
    internal void PublishChanges()
    {
        var entries = Capture();
        if (last.SequenceEqual(entries)) return;
        last = entries;
        network.SendAll(new NetworkPlayerList(entries));
    }

    // Stops publication when the server session is disposed.
    public void Dispose()
    {
        disposed = true;
        broker.Unsubscribe<CampaignTick>(Tick);
        broker.Unsubscribe<PlayerCampaignSynchronized>(Joined);
        broker.Unsubscribe<NetworkPlayerPlatformName>(SetName);
    }
}
