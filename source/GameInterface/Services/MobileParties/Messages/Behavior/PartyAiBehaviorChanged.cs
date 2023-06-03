using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior
{
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
