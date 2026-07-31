using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

/// <summary>
/// Client asks the server to add its party to an ongoing siege camp.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestJoinSiegeCamp : ICommand
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }

    public NetworkRequestJoinSiegeCamp(string partyId, string settlementId)
    {
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
