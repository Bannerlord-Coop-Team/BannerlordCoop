using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// Server answer to a break-in continuation request. On approval the settlement entry was replicated first.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkBreakInContinuationApproved : IEvent
{
    [ProtoMember(1)]
    public string RequestId { get; }
    [ProtoMember(2)]
    public string SettlementId { get; }
    [ProtoMember(3)]
    public bool Approved { get; }

    public NetworkBreakInContinuationApproved(string requestId, string settlementId, bool approved)
    {
        RequestId = requestId;
        SettlementId = settlementId;
        Approved = approved;
    }
}
