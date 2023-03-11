using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Messages
{
    public readonly struct ControlledPartyTargetPositionUpdated : IEvent
    {
        public TargetPositionData TargetPositionData { get; }

        public ControlledPartyTargetPositionUpdated(string controlledHeroStringId, Vec2 targetPostion)
        {
            TargetPositionData = new TargetPositionData(
                controlledHeroStringId, 
                targetPostion.X, 
                targetPostion.Y);
        }
    }
}
