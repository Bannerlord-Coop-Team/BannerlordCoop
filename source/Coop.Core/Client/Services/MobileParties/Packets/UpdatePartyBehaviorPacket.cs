using Common.PacketHandlers;
using GameInterface.Services.MobileParties.Data;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Packets
{
    /// <summary>
    /// Packet containing data to update party behavior
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record UpdatePartyBehaviorPacket : IPacket
    {
        [ProtoMember(1)]
        public PartyBehaviorUpdateData BehaviorUpdateData { get; }

        public PacketType PacketType => PacketType.UpdatePartyBehavior;

        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableUnordered;

        public UpdatePartyBehaviorPacket(PartyBehaviorUpdateData behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}
