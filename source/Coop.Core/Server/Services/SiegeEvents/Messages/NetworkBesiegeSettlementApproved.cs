using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// Server answer to the requester's siege start. On approval the siege objects were already
/// replicated ahead of this message, so the requester can open its siege menus; on rejection the
/// requester stays where it was.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkBesiegeSettlementApproved : IEvent
{
    [ProtoMember(1)]
    public bool Approved { get; }

    public NetworkBesiegeSettlementApproved(bool approved)
    {
        Approved = approved;
    }
}
