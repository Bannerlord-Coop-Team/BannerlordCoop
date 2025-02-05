using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkCreateMapEventSide(string MapEventSideId, string MapEventId, int BattleSide, string MobilePartyId) : IEvent
{
    [ProtoMember(1)]
    public string MapEventSideId { get; } = MapEventSideId;
    [ProtoMember(2)]
    public string MapEventId { get; } = MapEventId;
    [ProtoMember(3)]
    public int BattleSide { get; } = BattleSide;
    [ProtoMember(4)]
    public string MobilePartyId { get; } = MobilePartyId;
}