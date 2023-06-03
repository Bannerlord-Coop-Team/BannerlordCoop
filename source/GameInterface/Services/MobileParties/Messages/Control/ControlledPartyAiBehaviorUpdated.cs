using Common.Messaging;
using GameInterface.Services.MobileParties.Data;

namespace GameInterface.Services.MobileParties.Messages.Control
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
