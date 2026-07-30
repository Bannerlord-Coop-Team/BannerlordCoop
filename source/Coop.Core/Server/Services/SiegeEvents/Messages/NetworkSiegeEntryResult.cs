using Common.Messaging;
using GameInterface.Services.SiegeEvents.Validation;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

public enum SiegeEntryRequestType
{
    Besiege,
    Join,
    Reconnect,
}

public enum SiegeEntryOutcome
{
    Applied,
    Rejected,
}

/// <summary>
/// Reports the authoritative result of a siege entry attempt or reconnect validation, including
/// the state the requesting client should display after applying earlier ordered world updates.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkSiegeEntryResult : IEvent
{
    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string RequestedSettlementId { get; }
    [ProtoMember(3)]
    public string InteractionId { get; }
    [ProtoMember(4)]
    public SiegeEntryRequestType RequestType { get; }
    [ProtoMember(5)]
    public SiegeEntryOutcome Outcome { get; }
    [ProtoMember(6)]
    public SiegeEntryDenialReason Reason { get; }
    [ProtoMember(7)]
    public SiegeEntryDisposition Disposition { get; }
    [ProtoMember(8)]
    public string CanonicalSettlementId { get; }

    public NetworkSiegeEntryResult(
        string partyId,
        string requestedSettlementId,
        string interactionId,
        SiegeEntryRequestType requestType,
        SiegeEntryOutcome outcome,
        SiegeEntryDenialReason reason,
        SiegeEntryDisposition disposition,
        string canonicalSettlementId)
    {
        PartyId = partyId;
        RequestedSettlementId = requestedSettlementId;
        InteractionId = interactionId;
        RequestType = requestType;
        Outcome = outcome;
        Reason = reason;
        Disposition = disposition;
        CanonicalSettlementId = canonicalSettlementId;
    }
}
