using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Message from the client requesting a settlement encounter to end
/// </summary>
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
