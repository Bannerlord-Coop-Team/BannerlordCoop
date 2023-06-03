using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages
{
    /// <summary>
    /// Grants a client the control of a mobile party.
    /// </summary>
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
