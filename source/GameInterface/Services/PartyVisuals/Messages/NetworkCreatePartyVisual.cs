using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyVisuals.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkCreatePartyVisual : ICommand
    {
        [ProtoMember(1)]
        public string PartyVisualId { get; }

        [ProtoMember(2)]
        public string PartyBaseId { get; }

        public NetworkCreatePartyVisual(string partyVisualId, string partyBaseId)
        {
            PartyVisualId = partyVisualId;
            PartyBaseId = partyBaseId;
        }
    }
}
