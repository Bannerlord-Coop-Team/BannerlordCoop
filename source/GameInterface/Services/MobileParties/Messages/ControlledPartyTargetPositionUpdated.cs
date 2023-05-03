using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using System;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Messages
{
    public readonly struct ControlledPartyTargetPositionUpdated : IEvent
    {
        public TargetPositionData TargetPositionData { get; }

        public ControlledPartyTargetPositionUpdated(string partyId, Vec2 targetPostion)
        {
            TargetPositionData = new TargetPositionData(
                partyId, 
                targetPostion.X, 
                targetPostion.Y);
        }
    }
}
