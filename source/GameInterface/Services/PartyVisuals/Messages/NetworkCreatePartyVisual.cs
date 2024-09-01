using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyVisuals.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkCreatePartyVisual : ICommand
    {
        public NetworkCreatePartyVisual(string mobilePartyId)
        {
            MobilePartyId = mobilePartyId;
        }

        [ProtoMember(1)]
        public string MobilePartyId { get; }
    }
}
