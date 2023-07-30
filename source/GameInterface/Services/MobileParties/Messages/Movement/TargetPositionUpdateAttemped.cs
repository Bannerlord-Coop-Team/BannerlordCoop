using Common.Messaging;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Messages.Movement;

public record TargetPositionUpdateAttempted : IEvent
{
    public string PartyId { get; }

    public Vec2 TargetPosition { get; }

    public TargetPositionUpdateAttempted(string partyId, Vec2 value)
    {
        PartyId = partyId;
        TargetPosition = value;
    }
}
