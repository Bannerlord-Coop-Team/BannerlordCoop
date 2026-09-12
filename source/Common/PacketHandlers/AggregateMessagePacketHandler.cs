using Common.Logging;
using Common.Serialization;
using LiteNetLib;
using ProtoBuf;
using Serilog;
using System;

namespace Common.PacketHandlers;

/// <summary>
/// Receive side of message aggregation: unpacks an <see cref="AggregateMessagePacket"/> and publishes
/// each inner message, in order, exactly as if it had arrived as its own bare message.
/// </summary>
/// <remarks>
/// Aggregation exists because LiteNetLib's reliable channel allows only a small fixed window of
/// unacked packets in flight, so per-peer throughput is bounded by packets — not bytes — per round
/// trip. Batching many small messages into one packet is what keeps the world-sync stream under that
/// ceiling (see <c>CoopNetworkBase</c> for the send side).
/// </remarks>
public class AggregateMessagePacketHandler : IPacketHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AggregateMessagePacketHandler>();

    public PacketType PacketType => PacketType.AggregateMessage;

    private readonly IMessagePacketHandler messagePacketHandler;
    private readonly IPacketManager packetManager;
    private readonly ICommonSerializer serializer;

    public AggregateMessagePacketHandler(
        IMessagePacketHandler messagePacketHandler,
        IPacketManager packetManager,
        ICommonSerializer serializer)
    {
        this.messagePacketHandler = messagePacketHandler;
        this.packetManager = packetManager;
        this.serializer = serializer;
        this.packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var aggregatePacket = (AggregateMessagePacket)packet;
        if (aggregatePacket.Messages == null) return;

        foreach (var payload in aggregatePacket.Messages)
        {
            // Isolate failures per inner message: a bare message whose handler throws only loses
            // itself (the exception dies in the network poller), so an inner message must not take
            // the rest of its envelope down with it.
            try
            {
                var message = serializer.Deserialize<Messaging.IMessage>(payload);
                messagePacketHandler.PublishEvent(peer, message);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to handle a message inside an aggregate packet");
            }
        }
    }
}

/// <summary>
/// A batch of serialized messages sent as one reliable packet. Each entry is the exact byte payload
/// a bare message send would have put on the wire, in send order.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct AggregateMessagePacket : IPacket
{
    public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;

    public PacketType PacketType => PacketType.AggregateMessage;

    [ProtoMember(1)]
    public readonly byte[][] Messages;

    public AggregateMessagePacket(byte[][] messages)
    {
        Messages = messages;
    }
}
