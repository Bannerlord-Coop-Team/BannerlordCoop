using Common.Messaging;
using SandBox.View.Map;
using SandBox.View.Map.Visuals;

namespace GameInterface.Services.PartyVisuals.Messages
{
    internal record PartyVisualDestroyed : IEvent
    {
        public MobilePartyVisual MobilePartyVisual { get; }

        public PartyVisualDestroyed(MobilePartyVisual partyVisual)
        {
            MobilePartyVisual = partyVisual;
        }
    }
}
