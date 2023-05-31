using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Messages
{
    public record ControlledPartyAiBehaviorUpdated : IEvent
    {
        public AiBehaviorUpdateData BehaviorUpdateData { get; }

        public ControlledPartyAiBehaviorUpdated(AiBehaviorUpdateData behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}
