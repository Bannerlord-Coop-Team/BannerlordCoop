using Common.PacketHandlers;
using GameInterface.Services.MobileParties.Data;
using LiteNetLib;
using ProtoBuf;
using System;

namespace Coop.Core.Client.Services.MobileParties.Packets
{
    /// <summary>
    /// Packet containing data to update party behavior
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public struct UpdatePartyBehaviorPacket : IPacket
    {
        [ProtoMember(1)]
        public PartyBehaviorUpdateData[] BehaviorUpdateData { get; }

        public readonly PacketType PacketType => PacketType.UpdatePartyBehavior;

        public readonly DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableUnordered;

        public UpdatePartyBehaviorPacket(ref PartyBehaviorUpdateData[] behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}