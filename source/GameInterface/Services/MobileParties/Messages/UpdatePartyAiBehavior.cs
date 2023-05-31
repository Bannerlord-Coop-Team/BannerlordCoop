using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MobileParties.Messages
{
    public record UpdatePartyAiBehavior : ICommand
    {
        public AiBehaviorUpdateData BehaviorUpdateData { get; }

        public UpdatePartyAiBehavior(AiBehaviorUpdateData behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }

}
