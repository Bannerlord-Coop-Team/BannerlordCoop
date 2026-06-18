using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Core.Server.Services.Instances.Relay;

/// <summary>
/// Relay-fallback envelope. When a direct NAT punch between peers fails, a peer sends this to the server,
/// which forwards <see cref="Payload"/> to the connections of every controller named in
/// <see cref="ControllerIds"/> within <see cref="InstanceId"/>.
/// <para>
/// <see cref="Payload"/> is the <b>already-serialized</b> inner <see cref="IPacket"/> (raw bytes) so the
/// server relays it verbatim without deserializing — it only needs the instance + recipients to route.
/// (The serializer maps concrete top-level types by id and has no <c>[ProtoInclude]</c> for
/// <see cref="IPacket"/>, so a nested polymorphic packet could not round-trip anyway.) Carrying multiple
/// recipients per envelope keeps a broadcast to one serialize + one send-list instead of N envelopes.
/// </para>
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RelayPacket : IPacket
{
    public PacketType PacketType => PacketType.Relay;

    /// <summary>
    /// Delivery method the server forwards <see cref="Payload"/> with — set to the inner packet's own
    /// method so the relay keeps its QoS end to end. Stored (not a constant) so it round-trips to the server.
    /// </summary>
    [ProtoMember(1)]
    public DeliveryMethod DeliveryMethod { get; }

    /// <summary>Instance the recipients belong to (the client-derived settlement+location id).</summary>
    [ProtoMember(2)]
    public string InstanceId { get; }

    /// <summary>Controller ids to forward the payload to (multiple for a broadcast).</summary>
    [ProtoMember(3)]
    public string ControllerId { get; }

    /// <summary>The already-serialized inner packet, relayed verbatim.</summary>
    [ProtoMember(4)]
    public byte[] Payload { get; }

    public RelayPacket(DeliveryMethod deliveryMethod, string instanceId, string controllerId, byte[] payload)
    {
        DeliveryMethod = deliveryMethod;
        InstanceId = instanceId;
        ControllerId = controllerId;
        Payload = payload;
    }
}
