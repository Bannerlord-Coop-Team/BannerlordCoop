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
    [ProtoMember(2)]
    public bool BattleLeaveApplied { get; }

    public NetworkBreakSiegeApproved(bool approved, bool battleLeaveApplied)
    {
        Approved = approved;
        BattleLeaveApplied = battleLeaveApplied;
    }
}
