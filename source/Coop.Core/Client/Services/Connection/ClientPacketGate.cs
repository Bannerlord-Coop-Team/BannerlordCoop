using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;

namespace Coop.Core.Client.Services.Connection;

/// <summary>
/// Withholds world synchronization packets while the client is joining a session.
/// </summary>
/// <remarks>
/// The server broadcasts sync messages to every connected peer, including peers that
/// have not loaded the campaign yet. Applying those messages early mutates a
/// half-loaded campaign, and any missed object creations leave the client permanently
/// desynced. The transferred save is a snapshot of the host world, so ordered messages
/// received before <see cref="NetworkGameSaveDataReceived"/> are already part of the
/// save and are discarded, while packets received after it are held and replayed in
/// order once the campaign is loaded and all game objects are registered.
/// </remarks>
public interface IClientPacketGate : IDisposable
{
    /// <summary>
    /// Decides whether a received packet must be withheld from processing.
    /// </summary>
    /// <returns>True when the packet was withheld (discarded or held for later replay)</returns>
    bool TryHold(NetPeer peer, IPacket packet);
}

/// <inheritdoc cref="IClientPacketGate"/>
public class ClientPacketGate : IClientPacketGate
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientPacketGate>();

    private enum GateState
    {
        /// <summary>Joining, save snapshot not yet received: ordered sync messages are already in the snapshot</summary>
        Discarding,

        /// <summary>Save snapshot received: sync packets happened after it and are held for replay</summary>
        Holding,

        /// <summary>Campaign entered: every packet flows normally</summary>
        Open,
    }

    /// <summary>
    /// Messages required while joining. None of them touch campaign objects when handled
    /// (<see cref="NetworkNewPlayerHeroCreated"/> has its own deferral in RemotePlayerHeroHandler),
    /// and the state they carry is not part of the save snapshot, so they must never be
    /// discarded or delayed.
    /// </summary>
    private static readonly HashSet<Type> joinMessageTypes = new HashSet<Type>
    {
        typeof(NetworkClientValidated),
        typeof(NetworkModuleVersionsValidated),
        typeof(NetworkGameSaveDataReceived),
        typeof(NetworkHeroRecieved),
        typeof(NetworkNewPlayerHeroCreated),
        typeof(NetworkTimeControlLockChanged),
        typeof(NetworkMapEventLockChanged),
        typeof(SendInformationMessage),
    };

    private readonly IMessageBroker messageBroker;
    private readonly IPacketManager packetManager;
    private readonly ICommonSerializer serializer;

    // TryHold runs on the network poller thread while the release/reset handlers run on the
    // game main thread; every access to the mutable state below must hold this lock.
    private readonly object stateLock = new object();
    private GateState state = GateState.Discarding;
    private readonly List<(NetPeer Peer, IPacket Packet)> heldPackets = new List<(NetPeer, IPacket)>();
    private readonly HashSet<Type> loggedDiscardedTypes = new HashSet<Type>();
    private int discardedPacketCount = 0;

    public ClientPacketGate(
        IMessageBroker messageBroker,
        IPacketManager packetManager,
        ICommonSerializer serializer)
    {
        this.messageBroker = messageBroker;
        this.packetManager = packetManager;
        this.serializer = serializer;

        messageBroker.Subscribe<ReleaseNetworkBacklog>(Handle_ReleaseNetworkBacklog);
        messageBroker.Subscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ReleaseNetworkBacklog>(Handle_ReleaseNetworkBacklog);
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle_NetworkDisconnected);
    }

    public bool TryHold(NetPeer peer, IPacket packet)
    {
        // Unknown packets deserialize to null; pass them through so the failure
        // surfaces in the normal handling path instead of poisoning the replay
        if (packet is null) return false;

        lock (stateLock)
        {
            if (state == GateState.Open) return false;

            if (packet is MessagePacket messagePacket)
            {
                // Peeking instead of deserializing matters twice over: the payload is only
                // parsed once (by the normal pipeline), and no protobuf surrogates run their
                // object-manager lookups against the still-loading campaign
                if (serializer.TryPeekType(messagePacket.Data, out var messageType) == false) return false;

                if (joinMessageTypes.Contains(messageType))
                {
                    if (messageType == typeof(NetworkGameSaveDataReceived))
                    {
                        // The save snapshot has arrived; everything ordered after it must be
                        // held and replayed once the campaign is ready
                        state = GateState.Holding;
                    }

                    return false;
                }

                if (state == GateState.Discarding)
                {
                    // An ordered message sent before the host packaged its save is already part
                    // of the transferred snapshot; applying it again would duplicate the change
                    CountDiscarded(messageType);
                    return true;
                }
            }

            // Held packets: ordered messages sent after the snapshot, plus every non-message
            // packet (e.g. party behavior streams). The latter are unordered relative to the
            // save snapshot, so they are held even while discarding: replaying them in arrival
            // order converges on the newest state, while discarding could lose the final
            // update for a party that never changes behavior again
            heldPackets.Add((peer, packet));
            return true;
        }
    }

    private void CountDiscarded(Type messageType)
    {
        discardedPacketCount++;

        // First occurrence per type, so a join-flow message missing from the allowlist
        // leaves a trace even if the join never completes
        if (loggedDiscardedTypes.Add(messageType))
        {
            Logger.Information("Discarding pre-snapshot {MessageType} received while joining", messageType.Name);
        }
    }

    internal void Handle_ReleaseNetworkBacklog(MessagePayload<ReleaseNetworkBacklog> payload)
    {
        // Replaying inside the lock blocks the network poller until the backlog is applied,
        // so packets arriving mid-replay cannot overtake the packets held before them
        lock (stateLock)
        {
            if (state == GateState.Open) return;
            state = GateState.Open;

            Logger.Information(
                "Replaying {HeldCount} packets received while loading ({DiscardedCount} pre-snapshot messages discarded)",
                heldPackets.Count, discardedPacketCount);

            foreach (var (peer, packet) in heldPackets)
            {
                try
                {
                    packetManager.HandleReceive(peer, packet);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to replay a held {PacketType} packet", packet.PacketType);
                }
            }

            heldPackets.Clear();
            loggedDiscardedTypes.Clear();
            discardedPacketCount = 0;
        }
    }

    internal void Handle_NetworkDisconnected(MessagePayload<NetworkDisconnected> payload)
    {
        lock (stateLock)
        {
            state = GateState.Discarding;
            heldPackets.Clear();
            loggedDiscardedTypes.Clear();
            discardedPacketCount = 0;
        }
    }
}
