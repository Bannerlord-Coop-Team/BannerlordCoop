using Common.Messaging;
using Common.PacketHandlers;
using Common.Tests.Utils;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Extensions;
using LiteNetLib;
using Missions;

namespace E2E.Tests.Environment.Mock;

public sealed class DirectPacketSend
{
    public string ControllerId { get; }
    public IPacket Packet { get; }

    public DirectPacketSend(string controllerId, IPacket packet)
    {
        ControllerId = controllerId;
        Packet = packet;
    }
}

public sealed class SerializedPacketSend
{
    public string ControllerId { get; }
    public IPacket Packet { get; }
    public byte[] Payload { get; }

    public SerializedPacketSend(
        string controllerId,
        IPacket packet,
        byte[] payload)
    {
        ControllerId = controllerId;
        Packet = packet;
        Payload = payload;
    }
}

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
    public MessageCollection NetworkSentMessages { get; } = new MessageCollection();
    public PacketCollection NetworkSentPackets { get; } = new PacketCollection();
    public List<DirectPacketSend> DirectPacketSends { get; } = new();
    public List<SerializedPacketSend> SerializedPacketSends { get; } = new();
    public int MaxUnreliablePayloadBytes { get; set; } = 1000;
    public Dictionary<string, int> ControllerPayloadBytes { get; } = new();
    public bool RouteMessages { get; set; } = true;

    public MockBattleNetwork(MeshNetworkRouter router)
    {
        this.router = router;
    }

    public void Start() { }
    public void Stop() { }
    public void ConnectToInstance(string instanceId) { }

    public void SendAll(IMessage message)
    {
        NetworkSentMessages.Add(message);
        if (RouteMessages)
            router.SendAll(this, message);
    }

    public void Send(string controllerId, IMessage message)
    {
        NetworkSentMessages.Add(message);
        if (RouteMessages)
            router.Send(this, controllerId, message);
    }

    public void SendAllBut(string controllerId, IMessage message)
    {
        NetworkSentMessages.Add(message);
        if (RouteMessages)
            router.SendAllBut(this, controllerId, message);
    }

    // Packet broadcasts are captured for sender-path assertions; packet-level mesh routing isn't exercised.
    public void SendAll(IPacket packet) => NetworkSentPackets.Add(packet);
    public void SendAll(IPacket packet, byte[] serializedPacket)
    {
        NetworkSentPackets.Add(packet);
        SerializedPacketSends.Add(new SerializedPacketSend(null, packet, serializedPacket));
    }
    public void Send(string controllerId, IPacket packet)
    {
        NetworkSentPackets.Add(packet);
        DirectPacketSends.Add(new DirectPacketSend(controllerId, packet));
    }
    public void Send(string controllerId, IPacket packet, byte[] serializedPacket)
    {
        NetworkSentPackets.Add(packet);
        DirectPacketSends.Add(new DirectPacketSend(controllerId, packet));
        SerializedPacketSends.Add(
            new SerializedPacketSend(controllerId, packet, serializedPacket));
    }
    public void SendAllBut(string controllerId, IPacket packet) => throw new NotImplementedException();
    public int GetMaxUnreliablePayloadBytes(string controllerId) =>
        ControllerPayloadBytes.TryGetValue(controllerId, out int payloadBytes)
            ? payloadBytes
            : MaxUnreliablePayloadBytes;
    public int GetMaxUnreliablePayloadBytes() => MaxUnreliablePayloadBytes;
}
