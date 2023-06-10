using Common.PacketHandlers;
using GameInterface.Services.MobileParties.Data;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Packets
{
    [ProtoContract(SkipConstructor = true)]
    public record RequestMobilePartyBehaviorPacket : IPacket
    {
        public PacketType PacketType => PacketType.RequestMobilePartyBehavior;
        public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableUnordered;
        [ProtoMember(1)]
        public PartyBehaviorUpdateData BehaviorUpdateData { get; }

        public RequestMobilePartyBehaviorPacket(PartyBehaviorUpdateData behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}
