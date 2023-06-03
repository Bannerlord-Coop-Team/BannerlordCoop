using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.IntegrationTests.Environment.Extensions;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment.Mock;

public abstract class MockNetworkBase : INetwork
{
    private readonly TestNetworkOrchestrator networkOrchestrator;

    public static int InstanceCount = 0;

    public MockNetworkBase(TestNetworkOrchestrator networkOrchestrator)
    {
        this.networkOrchestrator = networkOrchestrator;

        InstanceCount = Interlocked.Increment(ref InstanceCount);

        NetPeer = NetPeerExtensions.CreatePeer(InstanceCount);
    }

    public INetworkConfiguration Configuration => throw new NotImplementedException();

    public int Priority => throw new NotImplementedException();

    public NetPeer NetPeer { get; } = NetPeerExtensions.CreatePeer();

    public MessageCollection NetworkSentMessages { get; } = new MessageCollection();

    public void Send(NetPeer netPeer, IPacket packet)
    {
        throw new NotImplementedException();
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        NetworkSentMessages.Add(message);

        networkOrchestrator.Send(NetPeer, netPeer, message);
    }

    public void SendAll(IPacket packet)
    {
        throw new NotImplementedException();
    }

    public void SendAll(IMessage message)
    {
        NetworkSentMessages.Add(message);

        networkOrchestrator.SendAll(NetPeer, message);
    }

    public void SendAllBut(NetPeer excludedPeer, IPacket packet)
    {
        throw new NotImplementedException();
    }

    public void SendAllBut(NetPeer excludedPeer, IMessage message)
    {
        NetworkSentMessages.Add(message);

        networkOrchestrator.SendAllBut(NetPeer, excludedPeer, message);
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
