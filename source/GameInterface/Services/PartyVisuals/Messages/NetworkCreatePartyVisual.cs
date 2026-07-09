using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyVisuals.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkCreatePartyVisual : ICommand
    {
        [ProtoMember(1)]
        public string PartyVisualId { get; }

        [ProtoMember(2)]
        public string MobilePartyId { get; }

        public NetworkCreatePartyVisual(string partyVisualId, string mobilePartyId)
        {
            PartyVisualId = partyVisualId;
            MobilePartyId = mobilePartyId;
        }
    }
}
