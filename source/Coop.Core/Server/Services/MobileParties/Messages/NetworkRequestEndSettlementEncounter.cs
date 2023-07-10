using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

[ProtoContract(SkipConstructor = true)]
internal record NetworkRequestEndSettlementEncounter : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }

    public NetworkRequestEndSettlementEncounter(string partyId)
    {
        PartyId = partyId;
    }
}
