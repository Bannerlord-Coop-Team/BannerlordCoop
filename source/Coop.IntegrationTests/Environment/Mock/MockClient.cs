using Coop.Core.Client;
using LiteNetLib;
using System.Net;
using System.Net.Sockets;

namespace Coop.IntegrationTests.Environment.Mock;

internal class MockClient : MockNetworkBase, ICoopClient
{
    public MockClient(TestNetworkRouter networkOrchestrator) : base(networkOrchestrator)
    {
    }

    public Guid ClientId => throw new NotImplementedException();

    public void OnConnectionRequest(ConnectionRequest request)
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
