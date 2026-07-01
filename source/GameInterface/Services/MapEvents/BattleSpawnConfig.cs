namespace GameInterface.Services.MapEvents;

/// <summary>
/// Toggles the Phase 2 host-authoritative battle spawn pipeline: disabling autonomous troop spawn on
/// non-host clients and replicating the host's spawns to peers as puppets. Off by default so the feature
/// is inert until deliberately enabled for a live battle test (turning it on without the rest of the
/// stack would leave non-hosts with no troops).
/// </summary>
public static class BattleSpawnConfig
{
    // ON for active host-authoritative battle testing. Set back to false to fully disable the coop battle
    // spawn/death/casualty pipeline (e.g. for a release build until it is proven).
    public static bool Enabled = true;
}
