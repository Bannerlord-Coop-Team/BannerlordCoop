using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntroServer.Server
{
    internal class PeerRegistry
    {
        private readonly Dictionary<string, List<P2PPeer>> m_instancePeers = new Dictionary<string, List<P2PPeer>>();
        private readonly Dictionary<string, NetPeer> m_peers = new Dictionary<string, NetPeer>();

        public NetPeer GetPeer(string id)
        {
            if (m_peers.TryGetValue(id, out NetPeer value))
            {
                return value;
            }
            return null;
        }

        public void RegisterPeer(string instance, P2PPeer peer)
        {
            if (m_instancePeers.TryGetValue(instance, out List<P2PPeer> peers))
            {
                peers.Add(peer);
            }
            else
            {
                m_instancePeers.Add(instance, new List<P2PPeer> { peer });
            }
        }

        public void RegisterPeer(string id, NetPeer peer)
        {
            if (m_peers.ContainsKey(id) == false)
            {
                m_peers.Add(id, peer);
            }
        }


        public void RemovePeer(string id)
        {
            if (m_peers.TryGetValue(id, out NetPeer peer))
            {
                RemovePeer(peer);
            }
        }

        public void RemovePeer(NetPeer peer)
        {
            foreach (var instancePeers in m_instancePeers)
            {
                var peers = instancePeers.Value;
                P2PPeer peerToRemove = peers.SingleOrDefault(p => p.NetPeer == peer);
                if (peerToRemove != null)
                {
                    peers.Remove(peerToRemove);
                }
            }

            string id = m_peers.Where(e => e.Value == peer).Select(e => e.Key).SingleOrDefault();

            if (id != null)
            {
                m_peers.Remove(id);
            }
        }

        public bool ContainsP2PPeer(string instance, string id)
        {
            if (m_instancePeers.TryGetValue(instance, out var p2PPeers) &&
               m_peers.TryGetValue(id, out var NetPeer))
            {
                return p2PPeers.Where(p => p.NetPeer == NetPeer).Count() > 0;
            }
            return false;
        }

        public IEnumerable<P2PPeer> GetPeersInInstance(string instance)
        {
            if (m_instancePeers.TryGetValue(instance, out List<P2PPeer> peers))
            {
                return peers;
            }

            return new P2PPeer[0];
        }
    }
}
