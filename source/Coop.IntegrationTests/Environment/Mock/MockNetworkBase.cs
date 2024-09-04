using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.IntegrationTests.Environment.Extensions;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment.Mock;

public abstract class MockNetworkBase : INetwork
{
    private readonly TestNetworkRouter networkOrchestrator;
    private readonly IPacketManager packetManager;
    public static int InstanceCount = 0;

    public MockNetworkBase(TestNetworkRouter networkOrchestrator, IPacketManager packetManager)
    {
        this.networkOrchestrator = networkOrchestrator;
        this.packetManager = packetManager;
        InstanceCount = Interlocked.Increment(ref InstanceCount);

        NetPeer = NetPeerExtensions.CreatePeer(InstanceCount);
    }

    public INetworkConfiguration Configuration => throw new NotImplementedException();

    public int Priority => throw new NotImplementedException();

    public NetPeer NetPeer { get; } = NetPeerExtensions.CreatePeer();

    public MessageCollection NetworkSentMessages { get; } = new MessageCollection();
    public PacketCollection NetworkSentPackets { get; } = new PacketCollection();

    public void ReceiveFromNetwork(NetPeer peer, IPacket packet) => packetManager.HandleReceive(peer, packet);

    public void Send(NetPeer netPeer, IPacket packet)
    {
        NetworkSentPackets.Add(packet);

        networkOrchestrator.Send(NetPeer, netPeer, packet);
    }

    public void Send(NetPeer netPeer, IMessage message)
    {
        NetworkSentMessages.Add(message);

        networkOrchestrator.Send(NetPeer, netPeer, message);
    }

    public void SendAll(IPacket packet)
    {
        NetworkSentPackets.Add(packet);

        networkOrchestrator.SendAll(NetPeer, packet);
    }

    public void SendAll(IMessage message)
    {
        NetworkSentMessages.Add(message);

        networkOrchestrator.SendAll(NetPeer, message);
    }

    public void SendAllBut(NetPeer excludedPeer, IPacket packet)
    {
        NetworkSentPackets.Add(packet);

        networkOrchestrator.SendAllBut(NetPeer, excludedPeer, packet);
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

    public void Dispose()
    {
    }
}
