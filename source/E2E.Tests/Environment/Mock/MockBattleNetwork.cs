using Common.Messaging;
using Common.PacketHandlers;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Extensions;
using LiteNetLib;
using Missions;

namespace E2E.Tests.Environment.Mock;

/// <summary>
/// Mock of the mission P2P mesh (<see cref="IBattleNetwork"/>) for E2E tests. The real mesh is a direct
/// client-to-client LiteNetLib link; this routes <see cref="IMessage"/> traffic between client instances
/// in-process via <see cref="MeshNetworkRouter"/> — the mesh counterpart to <see cref="TestNetworkRouter"/>.
/// Registered as <see cref="IBattleNetwork"/> on each client, overriding the real <c>LiteNetP2PClient</c>.
/// </summary>
public class MockBattleNetwork : IBattleNetwork
{
    private readonly MeshNetworkRouter router;

    public NetPeer NetPeer { get; } = NetPeerExtensions.CreatePeer();
    public PacketCollection NetworkSentPackets { get; } = new PacketCollection();

    public MockBattleNetwork(MeshNetworkRouter router)
    {
        this.router = router;
    }

    public void Start() { }
    public void Stop() { }
    public void ConnectToInstance(string instanceId) { }

    public void SendAll(IMessage message) => router.SendAll(this, message);
    public void Send(string controllerId, IMessage message) => router.Send(this, controllerId, message);
    public void SendAllBut(string controllerId, IMessage message) => router.SendAllBut(this, controllerId, message);

    // Packet broadcasts are captured for sender-path assertions; packet-level mesh routing isn't exercised.
    public void SendAll(IPacket packet) => NetworkSentPackets.Add(packet);
    public void Send(string controllerId, IPacket packet) => throw new NotImplementedException();
    public void SendAllBut(string controllerId, IPacket packet) => throw new NotImplementedException();
}
