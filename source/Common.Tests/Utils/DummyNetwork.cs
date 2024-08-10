using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using LiteNetLib;

namespace Common.Tests.Utils;
internal class DummyNetwork : INetwork
{
    public INetworkConfiguration Configuration => throw new NotImplementedException();

    public int Priority => throw new NotImplementedException();

    public void Dispose()
    {
    }

    public void Send(NetPeer netPeer, IPacket packet)
    {
        throw new NotImplementedException();
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        throw new NotImplementedException();
    }

    public void SendAll(IPacket packet)
    {
        throw new NotImplementedException();
    }

    public void SendAll(IMessage message)
    {
        throw new NotImplementedException();
    }

    public void SendAllBut(NetPeer excludedPeer, IPacket packet)
    {
        throw new NotImplementedException();
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        throw new NotImplementedException();
    }

    public void Start()
    {
        throw new NotImplementedException();
    }

    public void Stop()
    {
        throw new NotImplementedException();
    }

    public void Update(TimeSpan frameTime)
    {
        throw new NotImplementedException();
    }
}
