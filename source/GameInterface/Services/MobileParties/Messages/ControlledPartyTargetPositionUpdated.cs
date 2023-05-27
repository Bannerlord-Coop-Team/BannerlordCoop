using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Messages;

public record ControlledPartyTargetPositionUpdated : IEvent
{
    public PartyPositionData TargetPositionData { get; }

    public ControlledPartyTargetPositionUpdated(string partyId, Vec2 targetPostion)
    {
        TargetPositionData = new PartyPositionData(
            partyId, 
            targetPostion);
    }
}
