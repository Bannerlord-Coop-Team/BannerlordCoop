using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// Server approved the requester joining a siege camp; the camp write was already replicated ahead
/// of this message, so the requester can open its siege menus.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkJoinSiegeCampApproved : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public bool Approved { get; }

    public NetworkJoinSiegeCampApproved(string settlementId, bool approved)
    {
        SettlementId = settlementId;
        Approved = approved;
    }
}
