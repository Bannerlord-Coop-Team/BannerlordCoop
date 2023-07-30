using Common.PacketHandlers;
using Coop.Core.Common.Services.MobileParties.Data;
using LiteNetLib;
using ProtoBuf;
using TaleWorlds.Library;

namespace Coop.Core.Client.Services.MobileParties.Packets
{
    /// <summary>
    /// Packet containing data to update party behavior
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record UpdatePartyMovementPacket : IPacket
    {
        [ProtoMember(1)]
        public MobilePartyMovementData MovementData { get; }

        public PacketType PacketType => PacketType.UpdateMobilePartyMovement;

        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableUnordered;
        public UpdatePartyMovementPacket(MobilePartyMovementData movementData)
        {
            MovementData = movementData;
        }
    }
}
