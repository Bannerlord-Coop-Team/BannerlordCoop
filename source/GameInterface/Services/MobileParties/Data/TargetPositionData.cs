using ProtoBuf;

namespace GameInterface.Services.MobileParties.Data;

[ProtoContract]
public readonly struct TargetPositionData
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public float TargetPositionX { get; }
    [ProtoMember(3)]
    public float TargetPositionY { get; }

    public TargetPositionData(string partyId, float targetPositionX, float targetPositionY)
    {
        PartyId = partyId;
        TargetPositionX = targetPositionX;
        TargetPositionY = targetPositionY;
    }
}
