using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkGrantPartyControl : ICommand
    {
        [ProtoMember(1)]
        public string PartyId;

        public NetworkGrantPartyControl(string partyId)
        {
            PartyId = partyId;
        }
    }
}
