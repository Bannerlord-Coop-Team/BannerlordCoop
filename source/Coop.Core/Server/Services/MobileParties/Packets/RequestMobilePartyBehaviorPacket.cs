using Common.PacketHandlers;
using GameInterface.Services.MobileParties.Data;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Packets;

/// <summary>
/// Party behavior update request from client to server
/// </summary>
[ProtoContract(SkipConstructor = true)]
public struct RequestMobilePartyBehaviorPacket : IPacket
{
    public PacketType PacketType => PacketType.RequestUpdatePartyBehavior;
    public DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableUnordered;
    [ProtoMember(1)]
    public PartyBehaviorUpdateData BehaviorUpdateData { get; }

    public RequestMobilePartyBehaviorPacket(PartyBehaviorUpdateData behaviorUpdateData)
    {
        BehaviorUpdateData = behaviorUpdateData;
    }
}