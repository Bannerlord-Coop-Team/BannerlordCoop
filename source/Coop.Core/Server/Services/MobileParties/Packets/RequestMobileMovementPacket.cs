using Common.PacketHandlers;
using Coop.Core.Common.Services.MobileParties.Data;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Packets;

/// <summary>
/// Party behavior update request from client to server
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record RequestMobileMovementPacket : IPacket
{
    public PacketType PacketType => PacketType.RequestMobilePartyMovement;
    public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableUnordered;
    [ProtoMember(1)]
    public MobilePartyMovementData MovementData { get; }

    public RequestMobileMovementPacket(MobilePartyMovementData movementData)
    {
        MovementData = movementData;
    }
}
