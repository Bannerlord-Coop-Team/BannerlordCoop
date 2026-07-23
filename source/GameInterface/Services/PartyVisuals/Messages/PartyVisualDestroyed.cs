using Common.Messaging;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Messages
{
    internal record PartyVisualDestroyed : IEvent
    {
        public MobilePartyVisual MobilePartyVisual { get; }
        public MobileParty MobileParty { get; }

        public PartyVisualDestroyed(MobilePartyVisual partyVisual, MobileParty mobileParty)
        {
            MobilePartyVisual = partyVisual;
            MobileParty = mobileParty;
        }
    }
}
