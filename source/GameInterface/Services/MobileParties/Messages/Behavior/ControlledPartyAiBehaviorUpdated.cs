using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior
{
    /// <summary>
    /// Represent an update in the <see cref="MobilePartyAi"/> behavior of a controlled party.
    /// </summary>
    /// <seealso cref="MobilePartyBehaviorHandler"/>
    public record ControlledPartyAiBehaviorUpdated : IEvent
    {
        public AiBehaviorUpdateData BehaviorUpdateData { get; }

        public ControlledPartyAiBehaviorUpdated(AiBehaviorUpdateData behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}
