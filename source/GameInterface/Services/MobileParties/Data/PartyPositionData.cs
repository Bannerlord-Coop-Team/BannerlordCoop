using ProtoBuf;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Data;

[ProtoContract]
public record PartyPositionData
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public Vec2 TargetPosition { get; }

    public PartyPositionData(string partyId, Vec2 targetPosition)
    {
        PartyId = partyId;
        TargetPosition = targetPosition;
    }
}
