#if DEBUG
using GameInterface.Services.MapEvents.Messages.Start;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>One-shot DEBUG rejection used to exercise the production client recovery path.</summary>
internal sealed class MapEventCreationDebugHook
{
    private string attackerId;
    private string defenderId;

    internal int RejectionCount { get; private set; }

    internal bool IsArmed => attackerId != null && defenderId != null;

    internal void Arm(string armedAttackerId, string armedDefenderId)
    {
        attackerId = armedAttackerId;
        defenderId = armedDefenderId;
    }

    internal bool TryConsume(NetworkRequestCreateMapEvent request)
    {
        var matchesArmedPair =
            (request.AttackerId == attackerId && request.DefenderId == defenderId) ||
            (request.AttackerId == defenderId && request.DefenderId == attackerId);
        if (!IsArmed || !matchesArmedPair)
            return false;

        attackerId = null;
        defenderId = null;
        RejectionCount++;
        return true;
    }

    internal void Clear()
    {
        attackerId = null;
        defenderId = null;
        RejectionCount = 0;
    }

    internal string Describe()
    {
        return IsArmed
            ? $"armed attacker={attackerId} defender={defenderId} rejections={RejectionCount}"
            : $"idle rejections={RejectionCount}";
    }
}
#endif
