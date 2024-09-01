using Common.Messaging;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Messages
{
    public record PartyVisualCreated : ICommand
    {
        public PartyVisualCreated(PartyVisual partyVisual, PartyBase party)
        {
            PartyVisual = partyVisual;
            Party = party;
        }

        public PartyVisual PartyVisual { get; }
        public PartyBase Party { get; }
    }
}
