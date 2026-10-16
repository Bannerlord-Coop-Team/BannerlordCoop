namespace Missions.Battles;

/// <summary>
/// Who sent an authority-owned mission state, under which battle host epoch, and at which of the sender's own revisions.
/// Receivers order each stream by it; whether the epoch is from a superseded hosting generation is still judged once per
/// message by <see cref="IHostEpochPolicy"/> at the receiver gate.
/// </summary>
public readonly struct AuthorityStamp
{
    public AuthorityStamp(string sender, int epoch, long revision)
    {
        Sender = sender;
        Epoch = epoch;
        Revision = revision;
    }

    public string Sender { get; }
    public int Epoch { get; }
    public long Revision { get; }

    /// <summary>
    /// Whether this stamp supersedes <paramref name="than"/>: a newer epoch does, and within one epoch a later revision or a
    /// different sender, which numbers its states from its own count.
    /// </summary>
    public bool IsNewer(AuthorityStamp than) =>
        Epoch != than.Epoch ? Epoch > than.Epoch : Sender != than.Sender || Revision > than.Revision;

    /// <summary>
    /// Why a hull state carrying this stamp must be dropped, or null. Only <paramref name="authority"/> sends, so only its
    /// revisions are ordered against <paramref name="accepted"/>; with <paramref name="followsHostEpoch"/> (AI hulls) a
    /// newer epoch restarts that order and an older one is stale, otherwise the epoch is ignored.
    /// </summary>
    public string Validate(string authority, string ownControllerId, AuthorityStamp accepted, bool followsHostEpoch)
    {
        var rejection = ValidateSender(Sender, authority, ownControllerId);
        if (rejection != null) return rejection;

        if (followsHostEpoch && Epoch < accepted.Epoch) return "stale_epoch";
        bool newEpoch = followsHostEpoch && Epoch > accepted.Epoch;
        if (!newEpoch && Revision <= accepted.Revision) return "stale";
        return null;
    }

    /// <summary>Why a state from <paramref name="sender"/> must be dropped for a hull <paramref name="authority"/> holds, or null.</summary>
    public static string ValidateSender(string sender, string authority, string ownControllerId)
    {
        if (authority == ownControllerId) return "own_hull";
        if (sender != authority) return "not_authority";
        return null;
    }
}
