namespace GameInterface.Services.MapEvents;

/// <summary>
/// Cross-assembly state for the siege mission patches. The mission host is the single authority for
/// the shared siege scenery — engine deployment placement and the machines themselves (rams, towers,
/// ballistas, gates, ladders) — because their vanilla simulation is driven by whatever agents each
/// machine happens to man locally, which diverges per client. The Missions battle controller keeps
/// <see cref="IsLocalAuthority"/> current, and the appliers raise <see cref="SuppressCapture"/> around
/// a received change so the capture patches don't echo. Both flags are only touched on the game thread.
/// </summary>
public static class SiegeMissionAuthorityGate
{
    public static bool IsLocalAuthority;

    /// <summary>True once the host election result is stored locally; until then IsLocalAuthority
    /// being false means "unknown", and irreversible steps (auto-deploys, machine deactivation) wait.</summary>
    public static bool IsAuthorityKnown;

    public static bool SuppressCapture;
}
