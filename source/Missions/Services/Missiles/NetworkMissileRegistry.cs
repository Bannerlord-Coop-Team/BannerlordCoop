using Common.Logging;
using Common.Messaging;
using LiteNetLib;
using Missions.Services.Missiles.Message;
using Missions.Services.Network.Messages;
using Serilog;
using System;
using System.Collections.Generic;

namespace Missions.Services.Missiles
{
    /// <summary>
    /// Missile Registry for missiles fired over the network
    /// Used to convert peer missile index for the _missiles array in the Mission class
    /// to a index that was created on the this client
    /// </summary>
    public interface INetworkMissileRegistry : IDisposable
    {
        /// <summary>
        /// Attempts to convert a peer index to a local index
        /// </summary>
        /// <param name="peer">Peer that registered the index</param>
        /// <param name="peerIdx">Index of the missile from the peer's perspective</param>
        /// <param name="localIdx">Index of the from this clients perspective</param>
        /// <returns></returns>
        bool TryGetIndex(NetPeer peer, int peerIdx, out int localIdx);
    }

    /// <inheritdoc />
    internal class NetworkMissileRegistry : INetworkMissileRegistry
    {
        private static readonly ILogger Logger = LogManager.GetLogger<NetworkMissileRegistry>();

        private readonly IMessageBroker messageBroker;

        private readonly Dictionary<NetPeer, PeerMissileIndexMap> peerMissileRegistries = new Dictionary<NetPeer, PeerMissileIndexMap>();

        public NetworkMissileRegistry(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<PeerConnected>(Handle_PeerConnected);
            messageBroker.Subscribe<PeerDisconnected>(Handle_PeerDisconnected);
            messageBroker.Subscribe<PeerMissileAdded>(Handle_PeerMissileAdded);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PeerConnected>(Handle_PeerConnected);
            messageBroker.Unsubscribe<PeerDisconnected>(Handle_PeerDisconnected);
            messageBroker.Unsubscribe<PeerMissileAdded>(Handle_PeerMissileAdded);
        }

        private void Handle_PeerDisconnected(MessagePayload<PeerDisconnected> obj)
        {
            peerMissileRegistries.Clear();
        }

        private void Handle_PeerConnected(MessagePayload<PeerConnected> obj)
        {
            var peer = obj.What.Peer;

            peerMissileRegistries.Add(peer, new PeerMissileIndexMap(peer));
        }

        private void Handle_PeerMissileAdded(MessagePayload<PeerMissileAdded> obj)
        {
            var payload = obj.What;
            var peer = payload.Peer;
            if (peerMissileRegistries.TryGetValue(peer, out var peerMissileRegistry))
            {

                peerMissileRegistry.RegisterIndex(payload.PeerMissileId, payload.LocalMissileId);
            }
            else
            {
                Logger.Warning("Tried to register added missile but peer was not registered in {peerRegistries}", peerMissileRegistries);
            }
        }

        /// <inheritdoc />
        public bool TryGetIndex(NetPeer peer, int peerIdx, out int localIdx)
        {
            localIdx = -1;
            if (peerMissileRegistries.TryGetValue(peer, out var peerMissileRegistry) == false)
            {
                return false;
            }

            if(peerMissileRegistry.TryGetValue(peerIdx, out localIdx) == false) { return false; }

            return true;
        }

        /// <summary>
        /// Index map for a single peer
        /// </summary>
        internal class PeerMissileIndexMap
        {
            public readonly NetPeer Peer;

            public PeerMissileIndexMap(NetPeer peer)
            {
                Peer = peer;
            }

            private readonly Dictionary<int, int> missileIndexMap = new Dictionary<int, int>();

            public void RegisterIndex(int peerIdx, int localIdx)
            {
                if (missileIndexMap.ContainsKey(peerIdx))
                {
                    missileIndexMap[peerIdx] = localIdx;
                }
                else
                {
                    missileIndexMap.Add(peerIdx, localIdx);
                }
            }

            public bool TryGetValue(int peerIdx, out int localIdx) => missileIndexMap.TryGetValue(peerIdx, out localIdx);
        }
    }
}
