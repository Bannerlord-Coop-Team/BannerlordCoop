using Common.Messaging;
using SandBox.View.Map;

namespace GameInterface.Services.PartyVisuals.Messages
{
    internal record PartyVisualDestroyed : IEvent
    {
        public PartyVisual PartyVisual { get; }

        public PartyVisualDestroyed(PartyVisual partyVisual)
        {
            PartyVisual = partyVisual;
        }
    }
}
