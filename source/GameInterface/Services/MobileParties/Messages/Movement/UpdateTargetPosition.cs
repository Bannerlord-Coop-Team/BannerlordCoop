using Common.Messaging;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Messages.Movement;

public class UpdateTargetPosition : ICommand
{
    public string PartyId { get; }
    public Vec2 TargetPosition { get; }

    public UpdateTargetPosition(string partyId, Vec2 targetPosition)
    {
        PartyId = partyId;
        TargetPosition = targetPosition;
    }
}
