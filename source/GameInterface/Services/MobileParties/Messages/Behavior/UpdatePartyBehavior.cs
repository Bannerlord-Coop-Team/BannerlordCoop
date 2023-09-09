using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Handlers;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior
{
    /// <summary>
    /// Updates <see cref="MobilePartyAi"/> behavior on the campaign map.
    /// </summary>
    /// <seealso cref="MobilePartyBehaviorHandler"/>
    public record UpdatePartyBehavior : ICommand
    {
        public PartyBehaviorUpdateData BehaviorUpdateData { get; }

        public UpdatePartyBehavior(PartyBehaviorUpdateData behaviorUpdateData)
        {
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}