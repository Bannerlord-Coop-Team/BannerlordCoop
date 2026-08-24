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
    public string? ControllerId { get; }
    public IPacket Packet { get; }
    public byte[] Payload { get; }

    public SerializedPacketSend(
        string? controllerId,
        IPacket packet,
        byte[] payload)
    {
        ControllerId = controllerId;
        Packet = packet;
        Payload = payload;
    }
}

/// <summary>
/// Mock of the mission P2P mesh (<see cref="IBattleNetwork"/>) for E2E tests. Lifecycle and traffic route
/// through <see cref="MeshNetworkRouter"/> while the collections retain sender-path assertions.
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
    public bool RoutePackets { get; set; } = true;
    public bool IsStarted { get; private set; }
    public string? ActiveInstanceId { get; private set; }

    public MockBattleNetwork(MeshNetworkRouter router)
    {
        if (router == null) throw new ArgumentNullException(nameof(router));
        this.router = router;
    }

    public void Start()
    {
        if (IsStarted) return;

        router.Start(this);
        IsStarted = true;
    }

    public void Stop()
    {
        router.Stop(this);
        ActiveInstanceId = null;
        IsStarted = false;
    }

    public void ConnectToInstance(string instanceId)
    {
        router.ConnectToInstance(this, instanceId);
        ActiveInstanceId = instanceId;
    }

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

    public void SendAll(IPacket packet)
    {
        NetworkSentPackets.Add(packet);
        if (RoutePackets)
            router.SendAll(this, packet);
    }

    public void SendAll(IPacket packet, byte[] serializedPacket)
    {
        NetworkSentPackets.Add(packet);
        SerializedPacketSends.Add(new SerializedPacketSend(null, packet, serializedPacket));
        if (RoutePackets)
            router.SendAll(this, packet);
    }

    public void Send(string controllerId, IPacket packet)
    {
        NetworkSentPackets.Add(packet);
        DirectPacketSends.Add(new DirectPacketSend(controllerId, packet));
        if (RoutePackets)
            router.Send(this, controllerId, packet);
    }

    public void Send(string controllerId, IPacket packet, byte[] serializedPacket)
    {
        NetworkSentPackets.Add(packet);
        DirectPacketSends.Add(new DirectPacketSend(controllerId, packet));
        SerializedPacketSends.Add(
            new SerializedPacketSend(controllerId, packet, serializedPacket));
        if (RoutePackets)
            router.Send(this, controllerId, packet);
    }

    public void SendAllBut(string controllerId, IPacket packet)
    {
        NetworkSentPackets.Add(packet);
        if (RoutePackets)
            router.SendAllBut(this, controllerId, packet);
    }

    public int GetMaxUnreliablePayloadBytes(string controllerId) =>
        ControllerPayloadBytes.TryGetValue(controllerId, out int payloadBytes)
            ? payloadBytes
            : MaxUnreliablePayloadBytes;

    public int GetMaxUnreliablePayloadBytes() => MaxUnreliablePayloadBytes;
}
