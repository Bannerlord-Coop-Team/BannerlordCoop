using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

/// <summary>
/// Client asks the server to start the wall assault of a settlement its party is besieging.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestSiegeAssault : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkRequestSiegeAssault(string partyId, string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
