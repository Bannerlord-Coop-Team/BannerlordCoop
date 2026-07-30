using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.SiegeEvents.Messages;

public enum SiegeBreakOutcome
{
    Rejected,
    Applied,
    AlreadyLeft,
}

/// <summary>
/// Server result for a request to leave a siege camp.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkBreakSiegeApproved : IEvent
{
    [ProtoMember(1)]
    public SiegeBreakOutcome Outcome { get; }

    /// <summary>
    /// Echo of the request's local-continuation flag.
    /// </summary>
    [ProtoMember(2)]
    public bool FinishLocalMenus { get; }

    /// <summary>
    /// True when the party was no longer in a camp because an active siege assault owned the leave.
    /// Its replicated battle-leave path performs the client cleanup.
    /// </summary>
    [ProtoMember(3)]
    public bool BattleLeaveApplied { get; }

    public NetworkBreakSiegeApproved(
        SiegeBreakOutcome outcome,
        bool finishLocalMenus = true,
        bool battleLeaveApplied = false)
    {
        Outcome = outcome;
        FinishLocalMenus = finishLocalMenus;
        BattleLeaveApplied = battleLeaveApplied;
    }
}
