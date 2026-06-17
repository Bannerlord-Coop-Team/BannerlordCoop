using LiteNetLib;
using System.Runtime.CompilerServices;

namespace GameInterface.Missions;



internal class ControllerRegistry : IControllerRegistry
{
    private readonly ConditionalWeakTable<string, NetPeer> idToPeer = new();
    private readonly ConditionalWeakTable<NetPeer, string> peerToId = new();

    public void Add(string controllerId,  NetPeer peer)
    {
        idToPeer.Add(controllerId, peer);
        peerToId.Add(peer, controllerId);
    }

    public void Remove(NetPeer peer)
    {
        if (peerToId.TryGetValue(peer, out var controllerId))
        {
            idToPeer.Remove(controllerId);
        }
    }

    public bool TryGetPeer(string controllerId, out NetPeer netPeer)
    {
        netPeer = null;

        return idToPeer.TryGetValue(controllerId, out netPeer);
    }
}
