using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Cross-assembly state for the siege mission patches. The mission host is the default authority for
/// the shared siege scenery — engine deployment placement and the machines themselves (rams, towers,
/// ballistas, gates, ladders) — because their vanilla simulation is driven by whatever agents each
/// machine happens to man locally, which diverges per client. A client that mans a ranged machine can
/// claim its simulation per machine (see SiegeMachineStateReplicator). The Missions battle controller
/// keeps <see cref="IsLocalAuthority"/> current, and the appliers raise <see cref="SuppressCapture"/>
/// around a received change so the capture patches don't echo.
/// </summary>
public static class SiegeMissionAuthorityGate
{
    /// <summary>This client is the mission host. Written on the game thread, read on the parallel
    /// tick threads (SiegeWeapon.TickAux via the authority patches), hence volatile like the claim sets.</summary>
    public static volatile bool IsLocalAuthority;

    /// <summary>True once the host election result is stored locally; until then IsLocalAuthority
    /// being false means "unknown", and irreversible steps (auto-deploys, machine deactivation) wait.</summary>
    public static bool IsAuthorityKnown;

    /// <summary>[Game thread] An applier is re-running vanilla code from a received change.</summary>
    public static bool SuppressCapture;

    // Per-machine claims layered over the host default. Swapped whole (copy-on-write) because
    // SiegeWeapon.TickAux runs on parallel tick threads while the game thread updates the claims.
    private static volatile HashSet<int> locallyClaimedMachines = new HashSet<int>();
    private static volatile HashSet<int> remotelyClaimedMachines = new HashSet<int>();

    /// <summary>Whether this client runs the vanilla simulation of a machine: the mission host for
    /// everything except the machines a client claimed by manning them.</summary>
    public static bool IsMachineSimulatedLocally(int machineId)
    {
        return IsLocalAuthority
            ? !remotelyClaimedMachines.Contains(machineId)
            : locallyClaimedMachines.Contains(machineId);
    }

    /// <summary>[Game thread] Replace the claim sets; the sets must not be mutated after this call.</summary>
    public static void SetClaimedMachines(HashSet<int> locallyClaimed, HashSet<int> remotelyClaimed)
    {
        locallyClaimedMachines = locallyClaimed;
        remotelyClaimedMachines = remotelyClaimed;
    }

    public static void ResetClaimedMachines()
    {
        SetClaimedMachines(new HashSet<int>(), new HashSet<int>());
        remoteAims.Clear();
    }

    // [Game thread] Aim targets received for machines simulated elsewhere; the HandleUserAiming
    // prefix re-asserts them each weapon tick so vanilla's own speed-limited approach turns the body.
    private static readonly Dictionary<int, (float Direction, float ReleaseAngle)> remoteAims = new Dictionary<int, (float, float)>();

    public static void SetRemoteAim(int machineId, float direction, float releaseAngle)
    {
        remoteAims[machineId] = (direction, releaseAngle);
    }

    public static bool TryGetRemoteAim(int machineId, out float direction, out float releaseAngle)
    {
        direction = 0f;
        releaseAngle = 0f;
        if (IsMachineSimulatedLocally(machineId)) return false;
        if (!remoteAims.TryGetValue(machineId, out var aim)) return false;

        direction = aim.Direction;
        releaseAngle = aim.ReleaseAngle;
        return true;
    }
}
