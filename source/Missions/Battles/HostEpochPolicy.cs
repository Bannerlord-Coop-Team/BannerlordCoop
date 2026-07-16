namespace Missions.Battles;

/// <summary>
/// BR-102 receiver policy for host-authority messages on the mission mesh. The server issues each
/// battle a host epoch (1 at election, +1 per host change) that every client stores with the
/// assignment; senders stamp host-authority messages with their stored epoch and receivers use this
/// policy to drop messages from a deposed hosting generation.
/// <para>
/// A message is stale when its epoch is strictly LOWER than the receiver's stored assignment OR
/// strictly LOWER than the highest epoch this receiver has ALREADY ACCEPTED. Everything else is
/// accepted:
/// <list type="bullet">
/// <item><c>messageEpoch == 0</c> — unstamped: the sender has no assignment yet (e.g. a claimant
/// broadcasting before the election result reached it) or predates the epoch stamping. There is
/// nothing to judge, and it advances no watermark.</item>
/// <item><c>localEpoch == 0</c> — the receiver has no assignment yet, so it cannot judge staleness
/// against its assignment; this is the documented self-healing convergence window. The accepted-epoch
/// watermark below still applies.</item>
/// <item><c>messageEpoch == localEpoch</c> — the current hosting generation. Rejecting it would
/// drop the live host's traffic.</item>
/// <item><c>messageEpoch &gt; localEpoch</c> — the sender heard about a migration before this
/// receiver did (server broadcasts race the P2P mesh). Dropping would silence the NEW host until
/// the assignment broadcast lands here; accept and let the assignment catch up.</item>
/// </list>
/// </para>
/// <para>
/// The accepted-epoch high-water mark closes an ordering gap the per-message assignment check alone
/// misses: a receiver still on epoch 1 accepts an epoch-3 message (3 &gt; 1), then — without the
/// watermark — would also accept a delayed epoch-2 message from a superseded generation (2 &gt; 1)
/// and apply that older siege state LAST. Once epoch N has been accepted, every later message below N
/// is stale. The watermark is per battle instance: a fresh policy is created per
/// <see cref="CoopBattleController"/> (per battle), so it never leaks across battles, and that ONE
/// instance is shared by the battle's siege replicators so a superseded generation is dropped
/// consistently across every host-authority message type.
/// </para>
/// </summary>
public interface IHostEpochPolicy
{
    /// <summary>
    /// True if a host-authority message stamped <paramref name="messageEpoch"/> is from a superseded
    /// hosting generation and must be dropped, given the receiver's stored assignment
    /// <paramref name="localEpoch"/>. A non-stale (accepted) stamped message advances this policy's
    /// accepted-epoch high-water mark, so any later message below it is judged stale. Call exactly
    /// once per received message at the receiver gate.
    /// </summary>
    bool IsStale(int messageEpoch, int localEpoch);
}

/// <inheritdoc cref="IHostEpochPolicy"/>
public class HostEpochPolicy : IHostEpochPolicy
{
    // Highest epoch this receiver has already accepted, across every host-authority message type it
    // gates (this instance is shared by a battle's siege replicators). Reset naturally per battle by
    // the per-battle lifetime of the owning controller. 0 = nothing stamped has been accepted yet.
    private int acceptedEpoch;

    public bool IsStale(int messageEpoch, int localEpoch)
    {
        // Unstamped: the sender has no assignment yet (or predates stamping) — nothing to judge, and
        // it carries no generation to advance the watermark.
        if (messageEpoch == 0) return false;

        // Stale if below our stored assignment (when we have one) OR below the highest generation we
        // have already accepted — a delayed message from a superseded hosting generation.
        if ((localEpoch != 0 && messageEpoch < localEpoch) || messageEpoch < acceptedEpoch)
            return true;

        // Accepted: remember the highest generation applied so a later, older message is dropped.
        if (messageEpoch > acceptedEpoch) acceptedEpoch = messageEpoch;
        return false;
    }
}
