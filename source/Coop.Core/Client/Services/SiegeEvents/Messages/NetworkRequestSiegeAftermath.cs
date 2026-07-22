using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

/// <summary>
/// Client asks the server to apply its siege aftermath choice (devastate, pillage or show mercy).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestSiegeAftermath : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }
    [ProtoMember(3)]
    public int AftermathType { get; }

    public NetworkRequestSiegeAftermath(string partyId, string settlementId, int aftermathType)
    {
        PartyId = partyId;
        SettlementId = settlementId;
        AftermathType = aftermathType;
    }
}
