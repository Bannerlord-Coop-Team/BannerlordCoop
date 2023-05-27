using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using System;

namespace GameInterface.Services.MobileParties.Messages;

public record UpdatePartyTargetPosition : ICommand
{
    public PartyPositionData TargetPositionData { get; }

    public UpdatePartyTargetPosition(PartyPositionData targetPositionData)
    {
        TargetPositionData = targetPositionData;
    }
}
