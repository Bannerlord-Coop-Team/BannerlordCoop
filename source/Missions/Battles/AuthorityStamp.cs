using System.Collections.Generic;
using System.Linq;

namespace Missions.Battles;

/// <summary>
/// Who sent an authority-owned mission state, under which battle host epoch, and at which of the sender's own revisions.
/// Receivers order each stream by it; whether the epoch is from a superseded hosting generation is still judged once per
/// message by <see cref="IHostEpochPolicy"/> at the receiver gate. Every stream also drops a sender that is not in the
/// mission, so a departed owner's late traffic over the mesh changes nothing after its departure arrived.
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
    public string Validate(string authority, string ownControllerId, AuthorityStamp accepted, bool followsHostEpoch,
        IReadOnlyCollection<string> controllersInMission)
    {
        var rejection = ValidateSender(Sender, authority, ownControllerId, controllersInMission);
        if (rejection != null) return rejection;

        if (followsHostEpoch && Epoch < accepted.Epoch) return "stale_epoch";
        bool newEpoch = followsHostEpoch && Epoch > accepted.Epoch;
        if (!newEpoch && Revision <= accepted.Revision) return "stale";
        return null;
    }

    /// <summary>Why a state from <paramref name="sender"/> must be dropped for a hull <paramref name="authority"/> holds, or null.</summary>
    public static string ValidateSender(string sender, string authority, string ownControllerId,
        IReadOnlyCollection<string> controllersInMission)
    {
        if (authority == ownControllerId) return "own_hull";
        if (sender != authority) return "not_authority";
        return ValidateMember(sender, controllersInMission);
    }

    /// <summary>
    /// The mission-membership gate: why traffic from <paramref name="sender"/> must be dropped because it is not one of the
    /// <paramref name="controllersInMission"/> (the server-announced members), or null. A peer's mesh link only opens after
    /// its announced entry, and a departed owner's traffic stays dropped until it enters again. Null members skip the gate,
    /// for a departed host's held state that is replayed on purpose before its AI hulls move.
    /// </summary>
    public static string ValidateMember(string sender, IReadOnlyCollection<string> controllersInMission) =>
        controllersInMission == null || controllersInMission.Contains(sender) ? null : "not_in_mission";
}
