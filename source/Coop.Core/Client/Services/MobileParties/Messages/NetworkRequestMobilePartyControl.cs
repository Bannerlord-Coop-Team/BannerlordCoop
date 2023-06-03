using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages
{
    /// <summary>
    /// Request control of a mobile party entity.
    /// </summary>
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
