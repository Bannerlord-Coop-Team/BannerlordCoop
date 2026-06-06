using Common.PacketHandlers;
using Coop.Core.Server;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;

namespace E2E.Tests.Environment.Mock;

public class MockServer : MockNetworkBase, ICoopServer
{
    public MockServer(TestNetworkRouter networkOrchestrator, IPacketManager packetManager) :
        base(networkOrchestrator, packetManager)
    {
    }

    public IEnumerable<NetPeer> ConnectedPeers => peers;
    private List<NetPeer> peers = new List<NetPeer>();

    public void AddPeer(NetPeer peer)
    {
        peers.Add(peer);
    }

    public void RemovePeer(NetPeer peer)
    {
        peers.Remove(peer);
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        throw new NotImplementedException();
    }

    public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
    {
        throw new NotImplementedException();
    }

    public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        throw new NotImplementedException();
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        throw new NotImplementedException();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        throw new NotImplementedException();
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        throw new NotImplementedException();
    }
}
