using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Messages.Behavior
{
    /// <summary>
    /// Updates <see cref="MobilePartyAi"/> behavior on the campaign map.
    /// </summary>
    /// <seealso cref="MobilePartyBehaviorHandler"/>
    [DontLogMessage]
    public record UpdatePartyBehavior : ICommand
    {
        public PartyBehaviorUpdateData BehaviorUpdateData { get; }

        public UpdatePartyBehavior(PartyBehaviorUpdateData behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }

    /// <summary>
    /// Notifies that PartyBehavior was updated
    /// </summary>
    /// <seealso cref="MobilePartyBehaviorHandler"/>
    [DontLogMessage]
    public record PartyBehaviorUpdated : IEvent
    {
        public PartyBehaviorUpdateData BehaviorUpdateData { get; }
        public PartyBehaviorUpdated(PartyBehaviorUpdateData behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}