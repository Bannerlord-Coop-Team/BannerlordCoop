using ProtoBuf;
using TaleWorlds.Library;

namespace Coop.Core.Common.Services.MobileParties.Data;

public enum MovementType
{
    TargetParty,
    TargetSettlement,
    TargetPosition,
}

[ProtoContract(SkipConstructor = true)]
public record MobilePartyMovementData
{
    [ProtoMember(1)]
    public MovementType MovementType { get; }

    [ProtoMember(2)]
    public string ControllerId { get; }
    [ProtoMember(3)]
    public string PartyId { get; }
    [ProtoMember(4)]
    public string TargetPartyId { get; }
    [ProtoMember(5)]
    public string TargetSettlement { get; }
    [ProtoMember(6)]
    public Vec2 TargetPosition { get; }

    public MobilePartyMovementData(
        MovementType movementType,
        string controllerId,
        string partyId,
        string targetPartyId,
        string targetSettlement,
        Vec2 targetPosition)
    {
        MovementType = movementType;
        ControllerId = controllerId;
        PartyId = partyId;
        TargetPartyId = targetPartyId;
        TargetSettlement = targetSettlement;
        TargetPosition = targetPosition;
    }
}
