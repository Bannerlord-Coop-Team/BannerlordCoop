using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyVisuals.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkDestroyPartyVisual : ICommand
    {
        [ProtoMember(1)]
        public string PartyVisualId { get; }

        public NetworkDestroyPartyVisual(string partyVisualId)
        {
            PartyVisualId = partyVisualId;
        }
    }
}
