using Common.Messaging;
using SandBox.View.Map;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Messages
{
    internal record PartyVisualCreated : IEvent
    {
        public MobilePartyVisual MobilePartyVisual { get; }
        public PartyBase PartyBase { get; }

        public PartyVisualCreated(MobilePartyVisual partyVisual, PartyBase partyBase)
        {
            MobilePartyVisual = partyVisual;
            PartyBase = partyBase;
        }
    }
}
