using Common.Messaging;
using SandBox.View.Map;

namespace GameInterface.Services.PartyVisuals.Messages
{
    public record PartyVisualDestroyed : ICommand
    {
        public PartyVisualDestroyed(PartyVisual partyVisual)
        {
            PartyVisual = partyVisual;
        }

        public PartyVisual PartyVisual { get; }
    }
}
