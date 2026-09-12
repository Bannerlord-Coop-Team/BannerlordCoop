using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.PacketHandlers;
using Common.Voice;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Instances;
using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.Voice;

internal sealed class ServerVoiceHandler : IPacketHandler
{
    private readonly IPacketManager packets;
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly IMissionManager missions;
    private readonly IMissionMembershipRegistry missionMembership;
    private readonly IVoiceRoutingState routing;
    private readonly IVoicePolicy policy;
    private readonly IVoiceClock clock;
    private readonly VoiceRanges ranges;
    private readonly Func<IVoiceTransitWindow> transitFactory;
    private readonly Dictionary<NetPeer, IVoiceTransitWindow> transit = new();
    private readonly Dictionary<NetPeer, string> connections = new();
    private readonly Dictionary<string, NetPeer> peers = new();
    private readonly Dictionary<string, VoicePosition> positions = new();
    private readonly ConcurrentQueue<(NetPeer peer, VoicePacket packet, long arrived, int generation)> pending = new();
    private readonly Dictionary<NetPeer, long> generations = new();
    private long nextGeneration;
    private int configurationGeneration;
    private volatile bool voiceEnabled;
    private int queued;
    private int scheduled;
    private volatile bool disposed;
    public PacketType PacketType => PacketType.Voice;

    public ServerVoiceHandler(IPacketManager packets, IMessageBroker broker, INetwork network,
        IPlayerManager players, IObjectManager objects, IMissionManager missions, IMissionMembershipRegistry missionMembership,
        IVoiceRoutingState routing,
        IVoicePolicy policy, IVoiceClock clock, IModConfig config, Func<IVoiceTransitWindow> transitFactory)
    {
        this.packets = packets;
        this.broker = broker;
        this.network = network;
        this.players = players;
        this.objects = objects;
        this.missions = missions;
        this.missionMembership = missionMembership;
        this.routing = routing;
        this.policy = policy;
        this.clock = clock;
        this.transitFactory = transitFactory;
        ranges = config.Data.Voice?.ToRanges() ?? new VoiceRanges();
        voiceEnabled = config.Data.ModOptions?.VoiceEnabled ?? true;
        broker.Subscribe<ModConfigApplied>(ConfigurationApplied);
        packets.RegisterPacketHandler(this);
        broker.Subscribe<VoiceContextChanged>(ContextChanged);
        broker.Subscribe<PlayerCampaignSynchronized>(Synchronized);
        broker.Subscribe<PlayerDisconnected>(Disconnected);
    }

    private void ConfigurationApplied(MessagePayload<ModConfigApplied> payload)
    {
        bool enabled = payload.What.ModOptions.VoiceEnabled;
        if (enabled == voiceEnabled) return;
        voiceEnabled = enabled;
        Interlocked.Increment(ref configurationGeneration);
        positions.Clear();
        routing.Clear();
        while (pending.TryDequeue(out _)) Interlocked.Decrement(ref queued);
    }

    private bool IsCurrentPeer(NetPeer peer, out GameInterface.Services.Players.Data.Player player)
    {
        return players.TryGetPlayer(peer, out player) &&
            players.TryGetPeer(player.ControllerId, out var current) && ReferenceEquals(current, peer);
    }

    private void Synchronized(MessagePayload<PlayerCampaignSynchronized> payload)
        => GameThread.RunSafe(() =>
        {
            if (!disposed && payload.What.PlayerId.ConnectionState == ConnectionState.Connected)
                network.SendImmediate(payload.What.PlayerId, new VoiceConfiguration { Ranges = ranges, SentAt = clock.Milliseconds });
        }, context: nameof(ServerVoiceHandler));

    private void ContextChanged(MessagePayload<VoiceContextChanged> payload)
    {
        if (payload.Who is not NetPeer peer) return;
        var point = payload.What.Position;
        int generation = Volatile.Read(ref configurationGeneration);
        GameThread.RunSafe(() =>
        {
            if (disposed || !voiceEnabled || generation != configurationGeneration ||
                peer.ConnectionState != ConnectionState.Connected || !IsCurrentPeer(peer, out var player)) return;
            if (!connections.TryGetValue(peer, out var key))
            {
                key = player.ControllerId;
                if (connections.Count >= 10 && !peers.ContainsKey(key)) return;
                if (peers.TryGetValue(key, out var previous))
                {
                    connections.Remove(previous);
                    transit.Remove(previous);
                    generations.Remove(previous);
                    routing.Remove(key);
                    positions.Remove(key);
                }
                generations[peer] = ++nextGeneration;
                connections[peer] = key;
                peers[key] = peer;
                transit[peer] = transitFactory();
                transit[peer].Accept(payload.What.SentAt, clock.Milliseconds);
            }
            if (routing.ChangeContext(key, point)) positions.Remove(key);
        }, context: nameof(ServerVoiceHandler));
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        if (disposed || !voiceEnabled || packet is not VoicePacket voice || !voice.IsValid) return;
        if (Interlocked.Increment(ref queued) > 100) { Interlocked.Decrement(ref queued); return; }
        pending.Enqueue((peer, voice, clock.Milliseconds, Volatile.Read(ref configurationGeneration)));
        ScheduleDrain();
    }

    private void ScheduleDrain()
    {
        if (Interlocked.CompareExchange(ref scheduled, 1, 0) == 0)
            GameThread.RunSafe(Drain, context: nameof(ServerVoiceHandler));
    }

    private void Drain()
    {
        try
        {
            // A stalled game frame must not send an audio backlog on recovery.
            int count = 0;
            while (++count <= 100 && pending.TryDequeue(out var item))
            {
                Interlocked.Decrement(ref queued);
                if (disposed || !voiceEnabled || item.generation != configurationGeneration || clock.Milliseconds - item.arrived > 100) continue;
                Route(item.peer, item.packet);
            }
        }
        finally
        {
            Interlocked.Exchange(ref scheduled, 0);
            if (!disposed && !pending.IsEmpty) ScheduleDrain();
        }
    }

    private VoicePosition ValidatePosition(NetPeer peer, VoicePosition point)
    {
        if (peer.ConnectionState != ConnectionState.Connected || !IsCurrentPeer(peer, out var player) ||
            !objects.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) return null;
        if (point.Context != VoicePosition.CampaignContext) return point;
        if (party.MapEvent != null || missionMembership.IsControllerInMission(player.ControllerId)) return null;
        var location = party.GetPosition2D;
        return new VoicePosition(point.Context, point.Epoch, location.X, location.Y, 0, point.CanSpeak, point.CanHear);
    }

    private void Route(NetPeer peer, VoicePacket packet)
    {
        if (!voiceEnabled || !connections.TryGetValue(peer, out var source) || !IsCurrentPeer(peer, out var player)) return;
        if (!transit[peer].Accept(packet.SentAt, clock.Milliseconds)) return;
        var position = ValidatePosition(peer, packet.Position);
        if (position == null) return;
        packet.Position = position;
        if (!routing.Update(source, packet, clock.Milliseconds)) return;
        positions[source] = position;
        if (packet.Audio.Length == 0) return;
        foreach (var route in routing.Route(source, packet, ranges, clock.Milliseconds))
        {
            if (!peers.TryGetValue(route.Connection, out var target) || !positions.TryGetValue(route.Connection, out var previous)) continue;
            var listener = ValidatePosition(target, previous);
            if (listener == null) continue;
            if (position.Context != VoicePosition.CampaignContext)
            {
                int separator = position.Context.IndexOf(':');
                if (separator < 0 || !players.TryGetPlayer(target, out var recipient) ||
                    !missions.TryGetRelayTarget(peer, position.Context.Substring(separator + 1), recipient.ControllerId, out var member) ||
                    member != target) continue;
            }
            float gain = policy.Gain(position, listener, ranges);
            if (gain <= 0) continue;
            // Speech must never enter the campaign join replay queue.
            network.SendImmediate(target, new VoicePacket
            {
                Position = position, Audio = packet.Audio, Sequence = packet.Sequence,
                StateSequence = packet.StateSequence, Speaker = source, StreamGeneration = generations[peer],
                ListenerEpoch = route.ListenerEpoch, Gain = gain, SentAt = clock.Milliseconds
            });
        }
    }

    private void Disconnected(MessagePayload<PlayerDisconnected> payload)
    {
        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() =>
        {
            if (!connections.TryGetValue(peer, out var key)) return;
            connections.Remove(peer);
            transit.Remove(peer);
            generations.Remove(peer);
            if (!peers.TryGetValue(key, out var current) || !ReferenceEquals(current, peer)) return;
            peers.Remove(key);
            positions.Remove(key);
            routing.Remove(key);
        }, context: nameof(ServerVoiceHandler));
    }

    public void Dispose()
    {
        disposed = true;
        packets.RemovePacketHandler(this);
        broker.Unsubscribe<ModConfigApplied>(ConfigurationApplied);
        broker.Unsubscribe<VoiceContextChanged>(ContextChanged);
        broker.Unsubscribe<PlayerCampaignSynchronized>(Synchronized);
        broker.Unsubscribe<PlayerDisconnected>(Disconnected);
        while (pending.TryDequeue(out _)) Interlocked.Decrement(ref queued);
    }
}
