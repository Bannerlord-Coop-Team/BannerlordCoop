namespace Missions.Battles;

/// <summary>
/// [Game thread] The host epoch whose AI hull authority this client has applied, which trails the session's epoch by the
/// queued hull move of a host change; hull conditions stamp and check it, and a routed hit waits for it.
/// </summary>
public interface IAppliedHostEpoch
{
    /// <summary>The applied host epoch, or 0 before this client knew any.</summary>
    int AppliedHostEpoch { get; }
}
