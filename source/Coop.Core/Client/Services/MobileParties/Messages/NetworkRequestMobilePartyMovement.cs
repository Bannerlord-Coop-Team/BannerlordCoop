using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

[ProtoContract]
public record NetworkRequestMobilePartyMovement : ICommand 
{
    [ProtoMember(1)]
    public PartyPositionData TargetPositionData { get; }

    public NetworkRequestMobilePartyMovement(PartyPositionData targetPositionData)
    {
        TargetPositionData = targetPositionData;
    }
}
