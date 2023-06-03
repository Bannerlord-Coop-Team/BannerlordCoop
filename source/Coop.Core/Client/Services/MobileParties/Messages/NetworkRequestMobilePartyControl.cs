using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkRequestMobilePartyControl : ICommand
    {
        [ProtoMember(1)]
        public string PartyId;

        public NetworkRequestMobilePartyControl(string partyId)
        {
            PartyId = partyId;
        }
    }
}
