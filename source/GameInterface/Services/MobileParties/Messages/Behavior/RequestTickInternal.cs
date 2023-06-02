using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior
{
    internal record RequestTickInternal : IEvent
    {
        public MobilePartyAi PartyAi { get; }

        public RequestTickInternal(MobilePartyAi partyAi)
        {
            PartyAi = partyAi;
        }
    }
}
