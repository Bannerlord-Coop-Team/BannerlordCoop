using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using LiteNetLib;
using Missions.Services.Network.Messages;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Missions.Services.Network
{
    /// <summary>
    /// This class stores events from a connected client that are not ready to be processed,
    /// When the client events can be processed this class will process those events
    /// </summary>
    public class EventQueueManager : MessagePacketHandler, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<EventQueueManager>();
        private readonly ICommonSerializer serializer;
        // Mutated from LiteNetLib's network pump (the Poller runs on background thread-pool threads), so
        // these must be concurrent: PeerConnected / PeerDisconnect / HandlePacket can interleave across
        // threads, and a plain Dictionary would corrupt or throw under that.
        readonly ConcurrentDictionary<NetPeer, ConcurrentQueue<IMessage>> Queues = new ConcurrentDictionary<NetPeer, ConcurrentQueue<IMessage>>();

        readonly ConcurrentDictionary<NetPeer, bool> ReadyPeers = new ConcurrentDictionary<NetPeer, bool>();

        public EventQueueManager(IMessageBroker messageBroker, IPacketManager packetManager, ICommonSerializer serializer) : base(messageBroker, packetManager, serializer)
        {
            packetManager.RegisterPacketHandler(this);

            messageBroker.Subscribe<PeerConnected>(Handle_PeerConnected);
            messageBroker.Subscribe<PeerDisconnected>(Handle_PeerDisconnect);
            messageBroker.Subscribe<PeerReady>(Handle_PeerReady);
            this.serializer = serializer;
        }

        private void Handle_PeerReady(MessagePayload<PeerReady> obj)
        {
            var peer = obj.What.Peer;

            if (ReadyPeers.TryGetValue(peer, out var isReady) == false)
            {
                Logger.Error("Tried to process queue for peer that was not registered {endpoint}", peer);
                return;
            }

            // Peer is already ready, there is no need to process prejoin event queue
            if (isReady) return;

            if (Queues.TryGetValue(peer, out var queue) == false)
            {
                Logger.Error("Tried to process queue for peer that was not registered {endpoint}", peer);
                return;
            }

            while (queue.IsEmpty == false)
            {
                if (queue.TryDequeue(out var message))
                {
                    HandlePacket(peer, (IPacket)message);
                }
            }

            ReadyPeers[peer] = true;
        }

        private void Handle_PeerConnected(MessagePayload<PeerConnected> obj)
        {
            var peer = obj.What.Peer;
            if (peer == null)
            {
                Logger.Warning("Peer was null when expecting non-null peer");
                return;
            }

            // NetPeer equality is by endpoint, so a reconnecting peer (or a duplicate NAT-punch
            // connection to the same endpoint) collides with any leftover entry. Dictionary.Add throws
            // on a duplicate key, and this runs in the swallowed network pump — which would silently
            // break packet delivery for that peer on rejoin. Assign idempotently with a fresh queue.
            bool wasAlreadyTracked = ReadyPeers.ContainsKey(peer);
            ReadyPeers[peer] = false;
            Queues[peer] = new ConcurrentQueue<IMessage>();

            // wasAlreadyTracked=true flags an endpoint collision (reconnect / duplicate NAT connection)
            // that the old Dictionary.Add would have thrown on — the suspected rejoin-failure cause.
            Logger.Information("[LocationSync] EventQueue +peer {peer} (wasAlreadyTracked={wasAlreadyTracked}, totalPeers={count})",
                peer, wasAlreadyTracked, ReadyPeers.Count);
        }

        private void Handle_PeerDisconnect(MessagePayload<PeerDisconnected> obj)
        {
            var peer = obj.What.NetPeer;

            bool wasTracked = ReadyPeers.TryRemove(peer, out _);
            Queues.TryRemove(peer, out _);

            Logger.Information("[LocationSync] EventQueue -peer {peer} (wasTracked={wasTracked}, totalPeers={count})",
                peer, wasTracked, ReadyPeers.Count);
        }

        public override void HandlePacket(NetPeer peer, IPacket packet)
        {
            if(ReadyPeers.TryGetValue(peer, out var ready))
            {
                if (ready)
                {
                    base.HandlePacket(peer, packet);
                }
                else
                {
                    MessagePacket convertedPacket = (MessagePacket)packet;

                    var message = serializer.Deserialize<IMessage>(convertedPacket.Data);

                    // Join + leave must be processed immediately rather than queued behind the
                    // not-ready gate — a leave especially, since the sender disconnects right after.
                    if (message is NetworkMissionJoinInfo || message is NetworkLeaveMission)
                    {
                        Logger.Information("[LocationSync] EventQueue: {messageType} from {peer} delivered immediately (peer not yet ready)", message.GetType().Name, peer);
                        base.HandlePacket(peer, packet);
                        return;
                    }

                    Logger.Debug("[LocationSync] EventQueue: queued {messageType} from not-ready peer {peer}", message?.GetType().Name, peer);
                    // The peer can be removed concurrently (disconnect on another pump thread) between the
                    // ReadyPeers check and here, so guard the queue lookup rather than risk a KeyNotFound.
                    if (Queues.TryGetValue(peer, out var queue))
                    {
                        queue.Enqueue(message);
                    }
                }
            }
            else
            {
                Logger.Error("Tried to process message for unconnected peer {endpoint}", peer);
            }
        }

        ~EventQueueManager() => Dispose();

        public override void Dispose()
        {
            base.Dispose();
            ReadyPeers.Clear();
            Queues.Clear();
        }
    }
}
