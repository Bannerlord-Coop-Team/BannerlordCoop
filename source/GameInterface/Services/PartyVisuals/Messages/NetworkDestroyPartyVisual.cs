using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PartyVisuals.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkDestroyPartyVisual : ICommand
    {
        public NetworkDestroyPartyVisual(string mobilePartyId)
        {
            MobilePartyId = mobilePartyId;
        }

        [ProtoMember(1)]
        public string MobilePartyId { get; }
    }
}
