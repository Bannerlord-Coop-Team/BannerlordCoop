using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Services.MobileParties.Handlers;

namespace GameInterface.Services.MobileParties.Messages.Behavior
{
    /// <summary>
    /// Indicates an internal request to change <see cref="MobilePartyAi"/> behavior.
    /// </summary>
    /// <seealso cref="MobilePartyBehaviorHandler"/>
    internal record PartyAiBehaviorChanged : IEvent
    {
        public MobileParty Party { get; }
        public AiBehaviorUpdateData BehaviorUpdateData { get; }

        public PartyAiBehaviorChanged(MobileParty party, AiBehaviorUpdateData behaviorUpdateData)
        {
            Party = party;
            BehaviorUpdateData = behaviorUpdateData;
        }
    }
}
