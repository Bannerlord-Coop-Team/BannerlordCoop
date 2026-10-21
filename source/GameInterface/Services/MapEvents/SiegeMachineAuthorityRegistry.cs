using System;
using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <summary>Per-client siege authority: the mission-host flag (written by CoopBattleController) and per-machine
/// claims and aims (written by SiegeMachineStateReplicator), read by the siege patches.</summary>
public interface ISiegeMachineAuthorityRegistry
{
    /// <summary>This client is the mission host. Read on the parallel tick threads (SiegeWeapon.TickAux).</summary>
    bool IsLocalAuthority { get; set; }

    /// <summary>True once the host election result is stored locally; until then a false
    /// <see cref="IsLocalAuthority"/> means "unknown", and irreversible steps wait.</summary>
    bool IsAuthorityKnown { get; set; }

    /// <summary>Whether this client runs the vanilla simulation of a machine: the mission host for
    /// everything except the machines claimed by a client interacting with them.</summary>
    bool IsMachineSimulatedLocally(int machineId);

    /// <summary>[Game thread] Replaces the machines claimed by this client and by other clients.</summary>
    void SetClaimedMachines(IEnumerable<int> locallyClaimed, IEnumerable<int> remotelyClaimed);

    /// <summary>[Game thread] Drops every machine claim and received aim target.</summary>
    void ResetClaimedMachines();

    /// <summary>[Game thread] Stores the aim target received for a machine simulated elsewhere.</summary>
    void SetRemoteAim(int machineId, float direction, float releaseAngle);

    /// <summary>[Game thread] Reads the received aim target of a machine this client doesn't simulate.</summary>
    bool TryGetRemoteAim(int machineId, out float direction, out float releaseAngle);

    /// <summary>[Game thread] Forgets the authority and every claim, so a stale siege never bleeds into the next.</summary>
    void Reset();
}

/// <inheritdoc cref="ISiegeMachineAuthorityRegistry"/>
public class SiegeMachineAuthorityRegistry : ISiegeMachineAuthorityRegistry
{
    // Written on the game thread, read on the parallel tick threads (SiegeWeapon.TickAux via the authority patches).
    private volatile bool isLocalAuthority;
    private volatile bool isAuthorityKnown;

    // Per-machine claims layered over the host default. Swapped whole (copy-on-write) because
    // SiegeWeapon.TickAux runs on parallel tick threads while the game thread updates the claims.
    private volatile HashSet<int> locallyClaimedMachines = new HashSet<int>();
    private volatile HashSet<int> remotelyClaimedMachines = new HashSet<int>();

    // Game-thread only: the HandleUserAiming prefix that reads it runs from RangedSiegeWeapon.OnTick, not
    // the parallel tick, and re-asserts the targets each weapon tick so vanilla's own approach turns the body.
    private readonly Dictionary<int, (float Direction, float ReleaseAngle)> remoteAims = new Dictionary<int, (float, float)>();

    /// <inheritdoc/>
    public bool IsLocalAuthority
    {
        get => isLocalAuthority;
        set => isLocalAuthority = value;
    }

    /// <inheritdoc/>
    public bool IsAuthorityKnown
    {
        get => isAuthorityKnown;
        set => isAuthorityKnown = value;
    }

    /// <inheritdoc/>
    public bool IsMachineSimulatedLocally(int machineId)
    {
        return isLocalAuthority
            ? !remotelyClaimedMachines.Contains(machineId)
            : locallyClaimedMachines.Contains(machineId);
    }

    /// <inheritdoc/>
    public void SetClaimedMachines(IEnumerable<int> locallyClaimed, IEnumerable<int> remotelyClaimed)
    {
        locallyClaimedMachines = new HashSet<int>(locallyClaimed);
        remotelyClaimedMachines = new HashSet<int>(remotelyClaimed);
    }

    /// <inheritdoc/>
    public void ResetClaimedMachines()
    {
        SetClaimedMachines(Array.Empty<int>(), Array.Empty<int>());
        remoteAims.Clear();
    }

    /// <inheritdoc/>
    public void SetRemoteAim(int machineId, float direction, float releaseAngle)
    {
        remoteAims[machineId] = (direction, releaseAngle);
    }

    /// <inheritdoc/>
    public bool TryGetRemoteAim(int machineId, out float direction, out float releaseAngle)
    {
        direction = 0f;
        releaseAngle = 0f;
        if (IsMachineSimulatedLocally(machineId)) return false;
        if (!remoteAims.TryGetValue(machineId, out var aim)) return false;

        direction = aim.Direction;
        releaseAngle = aim.ReleaseAngle;
        return true;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        isLocalAuthority = false;
        isAuthorityKnown = false;
        ResetClaimedMachines();
    }
}
