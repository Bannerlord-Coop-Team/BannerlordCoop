using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.SiegeEvents.Messages;

/// <summary>
/// Client asks the server to enter its party into the besieged settlement before continuing the break-in.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestBreakInContinuation : ICommand
{
    [ProtoMember(1)]
    public string RequestId { get; }
    [ProtoMember(2)]
    public string PartyId { get; }
    [ProtoMember(3)]
    public string SettlementId { get; }

    public NetworkRequestBreakInContinuation(string requestId, string partyId, string settlementId)
    {
        RequestId = requestId;
        PartyId = partyId;
        SettlementId = settlementId;
    }
}
