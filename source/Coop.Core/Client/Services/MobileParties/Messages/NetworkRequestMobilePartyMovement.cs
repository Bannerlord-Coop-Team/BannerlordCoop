using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;
using TaleWorlds.Library;

namespace Coop.Core.Client.Services.MobileParties.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkRequestMobilePartyMovement : ICommand 
{
    [ProtoMember(1)]
    public string PartyId { get; }

    [ProtoMember(2)]
    public float TargetX { get; }

    [ProtoMember(3)]
    public float TargetY { get; }

    public PartyPositionData TargetPositionData { 
        get => new PartyPositionData(PartyId, new Vec2(TargetX, TargetY)); 
    }

    public NetworkRequestMobilePartyMovement(PartyPositionData targetPositionData)
    {
        PartyId = targetPositionData.PartyId;
        TargetX = targetPositionData.TargetPosition.X;
        TargetY = targetPositionData.TargetPosition.Y;
    }
}
