namespace Missions.Battles;

/// <summary>
/// BR-102 receiver policy for host-authority messages on the mission mesh. The server issues each
/// battle a host epoch (1 at election, +1 per host change) that every client stores with the
/// assignment; senders stamp host-authority messages with their stored epoch and receivers use this
/// policy to drop messages from a deposed hosting generation.
/// <para>
/// Only a message stamped with a strictly LOWER epoch than the receiver's stored assignment is
/// stale. Everything else is accepted:
/// <list type="bullet">
/// <item><c>messageEpoch == 0</c> — unstamped: the sender has no assignment yet (e.g. a claimant
/// broadcasting before the election result reached it) or predates the epoch stamping. There is
/// nothing to judge.</item>
/// <item><c>localEpoch == 0</c> — the receiver has no assignment yet, so it cannot judge staleness;
/// this is the documented self-healing convergence window.</item>
/// <item><c>messageEpoch == localEpoch</c> — the current hosting generation. Rejecting it would
/// drop the live host's traffic.</item>
/// <item><c>messageEpoch &gt; localEpoch</c> — the sender heard about a migration before this
/// receiver did (server broadcasts race the P2P mesh). Dropping would silence the NEW host until
/// the assignment broadcast lands here; accept and let the assignment catch up.</item>
/// </list>
/// </para>
/// </summary>
public static class HostEpochPolicy
{
    public static bool IsStale(int messageEpoch, int localEpoch)
    {
        return messageEpoch != 0
            && localEpoch != 0
            && messageEpoch < localEpoch;
    }
}
