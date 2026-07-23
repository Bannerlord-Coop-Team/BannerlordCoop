using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

/// <summary>
/// Server approved the requester leaving its siege camp.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkBreakSiegeApproved : IEvent
{
    [ProtoMember(1)]
    public bool Approved { get; }

    /// <summary>
    /// Echo of the request's flag: when false the requester's native flow already ran its own menu
    /// continuation, so the approval must not finish the local encounter or exit a menu.
    /// </summary>
    [ProtoMember(2)]
    public bool FinishLocalMenus { get; }

    public NetworkBreakSiegeApproved(bool approved, bool finishLocalMenus)
    {
        Approved = approved;
        FinishLocalMenus = finishLocalMenus;
    }
}
