using Common.Messaging;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Messages
{
    internal record PartyVisualCreated : IEvent
    {
        public PartyVisual PartyVisual { get; }
        public PartyBase PartyBase { get; }

        public PartyVisualCreated(PartyVisual partyVisual, PartyBase partyBase)
        {
            PartyVisual = partyVisual;
            PartyBase = partyBase;
        }
    }
}
