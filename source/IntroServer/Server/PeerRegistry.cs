using LiteNetLib;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntroServer.Server
{
    internal class PeerRegistry
    {
        private readonly Dictionary<string, List<P2PPeer>> _instancePeers = new Dictionary<string, List<P2PPeer>>();
        private readonly Dictionary<Guid, NetPeer> _peers = new Dictionary<Guid, NetPeer>();
        private readonly ILogger<MissionTestServer> _logger;

        public PeerRegistry(ILogger<MissionTestServer> logger)
        {
            _logger = logger;
        }

        public NetPeer GetPeer(Guid id)
        {
            if (_peers.TryGetValue(id, out NetPeer value))
            {
                return value;
            }
            return null;
        }

        public void RegisterPeer(string instance, P2PPeer peer)
        {
            if (_instancePeers.TryGetValue(instance, out List<P2PPeer> peers))
            {
                peers.Add(peer);
            }
            else
            {
                _instancePeers.Add(instance, new List<P2PPeer> { peer });
            }

            _logger.LogDebug("Adding peer to {instance}, total peers in instance {PeerCount}", instance, _instancePeers[instance].Count);
        }

        public void RegisterPeer(Guid id, NetPeer peer)
        {
            if (_peers.ContainsKey(id) == false)
            {
                _peers.Add(id, peer);
            }
        }


        public void RemovePeer(Guid id)
        {
            if (_peers.TryGetValue(id, out NetPeer peer))
            {
                RemovePeer(peer);
            }
        }

        public void RemovePeer(NetPeer peer)
        {
            foreach (var instancePeers in _instancePeers)
            {
                var peers = instancePeers.Value;
                foreach(var peerToRemove in peers.Where(p => p.NetPeer == peer).ToList())
                {
                    peers.Remove(peerToRemove);
                }
            }

            Guid id = _peers.SingleOrDefault(kvp => kvp.Value == peer).Key;

            _peers.Remove(id);
        }

        public bool ContainsP2PPeer(string instance, Guid id)
        {
            if (_instancePeers.TryGetValue(instance, out var p2PPeers) &&
               _peers.TryGetValue(id, out var NetPeer))
            {
                return p2PPeers.Where(p => p.NetPeer == NetPeer).Count() > 0;
            }
            return false;
        }

        public IEnumerable<P2PPeer> GetPeersInInstance(string instance)
        {
            if (_instancePeers.TryGetValue(instance, out List<P2PPeer> peers))
            {
                return peers;
            }

            return Array.Empty<P2PPeer>();
        }
    }
}
