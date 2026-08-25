using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions;
using TaleWorlds.MountAndBlade.Objects.Usables;

namespace Missions.Battles;

/// <summary>
/// Replicates siege machine state between the mission clients. The mission host simulates every
/// machine by default; a client claims one by manning it, or is granted an idle one whose natural
/// crew is its troops. Simulators broadcast weapon/aim/movement state, damage stays host-owned.
/// </summary>
public interface ISiegeMachineStateReplicator : IDisposable
{
    /// <summary>[Game thread] Poll for changes and claim transitions.</summary>
    void Tick(float dt);

    /// <summary>[Host] Replay every machine's current state and claim to a joining controller.</summary>
    void CatchUpJoiner(string controllerId);

    /// <summary>[Game thread] Resolve the current simulator and authority order for an event stamp.</summary>
    bool TryGetMachineAuthority(
        int machineId,
        out string controllerId,
        out int hostEpoch,
        out int authorityRevision);

    event Action<int> AuthorityChanged;
}

/// <inheritdoc cref="ISiegeMachineStateReplicator"/>
public class SiegeMachineStateReplicator : ISiegeMachineStateReplicator
{
    private const float PollInterval = 0.25f;
    // Below vanilla's slowest ram speed so a moving machine updates every poll and the peer's
    // client-side easing never runs dry between messages.
    private const float MoveDistanceThreshold = 0.2f;
    // Past this the peer is not easing, it is somewhere else entirely (joiner catch-up) — snap.
    private const float MoveDistanceSnapThreshold = 2f;
    private const float ReleaseAfterUnusedSeconds = 2f;
    private const float ClaimRetrySeconds = 1f;
    // Crew-proximity grants: how far the host looks for a machine's natural crew, how many consecutive
    // polls the winner must hold before the grant, how long a fresh grantee gets to walk its troops
    // over, and how long an unused hand-back blocks re-granting the same machine.
    private const float CrewSearchRadius = 40f;
    private const int GrantStreakRequired = 3;
    private const float GrantGraceSeconds = 10f;
    private const float RegrantCooldownSeconds = 15f;
    // Aim replication: -1000 sentinel (a valid angle can be -1) and vanilla's own re-send threshold.
    private const float AimSentinel = -1000f;
    private const float AimEpsilon = 0.02f;
    private const float LadderAnimationSentinel = -1f;
    private const float LadderAnimationEpsilon = 0.01f;
    private const float LadderFrameEpsilon = 0.001f;

    private static readonly ILogger Logger = LogManager.GetLogger<SiegeMachineStateReplicator>();

    public event Action<int> AuthorityChanged;

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IHostEpochPolicy hostEpochPolicy;

    // Machine cache + last broadcast state per machine id / deactivated ids (peers).
    // Game-thread only; reset when the mission changes (MissionObjectIds recycle across missions).
    private readonly List<UsableMachine> machines = new List<UsableMachine>();
    private readonly Dictionary<int, UsableMachine> machinesById = new Dictionary<int, UsableMachine>();
    private readonly Dictionary<int, NetworkSiegeMachineState> lastSent = new Dictionary<int, NetworkSiegeMachineState>();
    private readonly Dictionary<int, NetworkSiegeLadderAnimationState> lastSentLadderAnimations =
        new Dictionary<int, NetworkSiegeLadderAnimationState>();
    private readonly HashSet<int> deactivated = new HashSet<int>();
    // States that arrived before their MissionObject registered (catch-up during a peer's scene load);
    // re-applied once the object appears. Keyed by machine id, latest state wins.
    private readonly Dictionary<int, NetworkSiegeMachineState> pendingByMachineId = new Dictionary<int, NetworkSiegeMachineState>();
    private readonly Dictionary<int, NetworkSiegeLadderAnimationState> pendingLadderAnimationsById =
        new Dictionary<int, NetworkSiegeLadderAnimationState>();
    // Peer-side: last RangedSiegeWeapon.WeaponState applied per machine, so the wind-up/reload animation fires
    // only on a state transition (the state message re-sends whenever any field, e.g. HitPoints, changes).
    private readonly Dictionary<int, int> peerWeaponState = new Dictionary<int, int>();
    // Per-machine simulation claims (machine id -> controller); absent = the mission host. The
    // patch-visible copies live in SiegeMissionAuthorityGate. Game-thread only.
    private readonly Dictionary<int, string> claimedMachines = new Dictionary<int, string>();
    private readonly Dictionary<int, int> authorityEpochs = new Dictionary<int, int>();
    private readonly Dictionary<int, int> authorityRevisions = new Dictionary<int, int>();
    private readonly Dictionary<int, string> authorityHostControllers = new Dictionary<int, string>();
    private readonly Dictionary<int, float> pendingClaimSeconds = new Dictionary<int, float>();
    private readonly Dictionary<int, float> unusedOwnedSeconds = new Dictionary<int, float>();
    // Non-primary machines whose local troop AI is currently gated off because another client simulates them.
    private readonly HashSet<int> aiDisabledMachines = new HashSet<int>();
    // Primary machines this client simulated last poll, so losing the sim vacates the local crew;
    // their AI flag is never touched (a disabled primary weapon crashes the attacker tactic).
    private readonly HashSet<int> locallySimulatedPrimaryMachines = new HashSet<int>();
    // [Host] Machines granted by crew proximity rather than a player mount; a mount claim outranks
    // these, and an unused hand-back starts a cooldown so a troopless winner can't flap the grant.
    private readonly HashSet<int> proximityGrants = new HashSet<int>();
    private readonly Dictionary<int, string> grantWinner = new Dictionary<int, string>();
    private readonly Dictionary<int, int> grantStreak = new Dictionary<int, int>();
    private readonly Dictionary<int, float> regrantCooldown = new Dictionary<int, float>();
    // [Peer] Grace left on an unsolicited (crew-proximity) grant before the unused-release clock starts.
    private readonly Dictionary<int, float> grantGrace = new Dictionary<int, float>();
    // [Host] Per-poll snapshot of grant-eligible agents, built once and reused for every candidate
    // machine instead of re-walking Mission.Agents per machine.
    private readonly List<CrewCandidate> crewCandidates = new List<CrewCandidate>();
    private bool crewSnapshotValid;
    private Mission trackedMission;
    private int trackedObjectCount;
    private float pollTimer;

    public SiegeMachineStateReplicator(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session, INetworkAgentRegistry agentRegistry, IHostEpochPolicy hostEpochPolicy)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;
        this.agentRegistry = agentRegistry;
        this.hostEpochPolicy = hostEpochPolicy;

        messageBroker.Subscribe<NetworkSiegeMachineState>(Handle_NetworkMachineState);
        messageBroker.Subscribe<NetworkSiegeLadderAnimationState>(Handle_NetworkLadderAnimationState);
        messageBroker.Subscribe<NetworkSiegeMachineClaim>(Handle_NetworkMachineClaim);
        messageBroker.Subscribe<NetworkSiegeMachineAuthority>(Handle_NetworkMachineAuthority);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSiegeMachineState>(Handle_NetworkMachineState);
        messageBroker.Unsubscribe<NetworkSiegeLadderAnimationState>(Handle_NetworkLadderAnimationState);
        messageBroker.Unsubscribe<NetworkSiegeMachineClaim>(Handle_NetworkMachineClaim);
        messageBroker.Unsubscribe<NetworkSiegeMachineAuthority>(Handle_NetworkMachineAuthority);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_MissionPeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_MissionPeerDisconnected);
    }

    public void Tick(float dt)
    {
        if (Mission.Current == null || !Mission.Current.IsSiegeBattle) return;

        pollTimer += dt;
        if (pollTimer < PollInterval) return;
        float elapsed = pollTimer;
        pollTimer = 0f;

        RefreshMachineCache();
        DrainPendingMachineStates();

        // Until the election result is stored, "not host" means "unknown" — take no irreversible step.
        if (!SiegeMissionAuthorityGate.IsAuthorityKnown) return;

        if (session.IsLocalHost)
        {
            ReannounceAuthorityAfterPromotion();
            ReactivateIfPromoted();
            EvaluateProximityCrewGrants(elapsed);
        }
        else
        {
            DeactivateNewMachines();
            ScanMachineClaims(elapsed);
        }

        RefreshMachineGates();
        BroadcastChangedStates();
    }

    private void RefreshMachineCache()
    {
        var mission = Mission.Current;
        if (trackedMission == mission && trackedObjectCount == mission.MissionObjects.Count) return;

        if (trackedMission != mission)
        {
            lastSent.Clear();
            lastSentLadderAnimations.Clear();
            deactivated.Clear();
            pendingByMachineId.Clear();
            pendingLadderAnimationsById.Clear();
            peerWeaponState.Clear();
            claimedMachines.Clear();
            authorityEpochs.Clear();
            authorityRevisions.Clear();
            authorityHostControllers.Clear();
            pendingClaimSeconds.Clear();
            unusedOwnedSeconds.Clear();
            aiDisabledMachines.Clear();
            locallySimulatedPrimaryMachines.Clear();
            proximityGrants.Clear();
            grantWinner.Clear();
            grantStreak.Clear();
            regrantCooldown.Clear();
            grantGrace.Clear();
            SiegeMissionAuthorityGate.ResetClaimedMachines();
        }

        trackedMission = mission;
        trackedObjectCount = mission.MissionObjects.Count;
        machines.Clear();
        machinesById.Clear();
        foreach (var missionObject in mission.MissionObjects)
        {
            if (missionObject is UsableMachine machine)
            {
                machines.Add(machine);
                machinesById[machine.Id.Id] = machine;
            }
        }
    }

    // Reset inherited revisions before the promoted host sends any authority or state.
    private void ReannounceAuthorityAfterPromotion()
    {
        var machineIds = new HashSet<int>(authorityRevisions.Keys);
        foreach (var machine in machines)
        {
            machineIds.Add(machine.Id.Id);
        }

        foreach (var machineId in machineIds)
        {
            NormalizeAuthorityForCurrentHost(machineId, initializeIfMissing: true);
        }
    }

    private void NormalizeAuthorityForCurrentHost(int machineId, bool initializeIfMissing)
    {
        int hostEpoch = session.HostEpoch;
        bool hasAuthority = authorityRevisions.ContainsKey(machineId);
        int currentEpoch = GetAuthorityEpoch(machineId);
        if ((!hasAuthority && !initializeIfMissing)
            || currentEpoch > hostEpoch
            || (currentEpoch == hostEpoch && GetAuthorityHostController(machineId) == session.OwnControllerId))
        {
            return;
        }

        authorityEpochs[machineId] = hostEpoch;
        authorityRevisions[machineId] = 0;
        authorityHostControllers[machineId] = session.OwnControllerId;
        lastSent.Remove(machineId);
        lastSentLadderAnimations.Remove(machineId);
        network.SendAll(new NetworkSiegeMachineAuthority(
            machineId,
            CurrentAuthorityOwner(machineId),
            hostEpoch,
            authorityRevision: 0,
            senderControllerId: session.OwnControllerId));
        AuthorityChanged?.Invoke(machineId);
    }

    // A successor promoted to host had deactivated its machines as a peer; the simulating
    // client must have live machines, so undo it once.

    private void ReactivateIfPromoted()
    {
        if (deactivated.Count == 0) return;

        foreach (var machineId in deactivated)
        {
            if (machinesById.TryGetValue(machineId, out var machine))
            {
                machine.Activate();
            }
        }

        Logger.Information("[BattleSync] Promoted to siege authority: reactivated {Count} machine(s)", deactivated.Count);
        deactivated.Clear();
    }

    private void BroadcastChangedStates()
    {
        bool isHost = session.IsLocalHost;
        foreach (var machine in machines)
        {
            int machineId = machine.Id.Id;
            bool simulatedLocally = SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machineId);
            if (!isHost && !simulatedLocally) continue;

            var state = CaptureState(machine, isHost, simulatedLocally);
            BroadcastMachineStateIfChanged(state);

            if (simulatedLocally && machine is SiegeLadder ladder)
            {
                var ladderAnimation = CaptureLadderAnimationState(ladder);
                if (!LadderAnimationStatesEqual(machineId, ladderAnimation))
                {
                    lastSentLadderAnimations[machineId] = ladderAnimation;
                    network.SendAll(Stamp(ladderAnimation));
                }
            }
        }
    }

    private void BroadcastMachineStateIfChanged(NetworkSiegeMachineState state)
    {
        if (MachineStatesEqual(state.MachineId, state)) return;

        lastSent[state.MachineId] = state;
        network.SendAll(Stamp(state));
    }

    private bool MachineStatesEqual(int machineId, NetworkSiegeMachineState state)
    {
        return lastSent.TryGetValue(machineId, out var previous)
            && previous.HitPoints == state.HitPoints
            && previous.DestructionState == state.DestructionState
            && previous.GateState == state.GateState
            && previous.LadderState == state.LadderState
            && previous.HasArrived == state.HasArrived
            && previous.WeaponState == state.WeaponState
            && previous.HasStoneAmmo == state.HasStoneAmmo
            && (!state.HasStoneAmmo || previous.StoneAmmo == state.StoneAmmo)
            && Math.Abs(previous.MoveDistance - state.MoveDistance) < MoveDistanceThreshold
            && Math.Abs(previous.AimDirection - state.AimDirection) < AimEpsilon
            && Math.Abs(previous.AimReleaseAngle - state.AimReleaseAngle) < AimEpsilon;
    }

    private bool LadderAnimationStatesEqual(int ladderId, NetworkSiegeLadderAnimationState state)
    {
        return lastSentLadderAnimations.TryGetValue(ladderId, out var previous)
            && Math.Abs(previous.AnimationSpeed - state.AnimationSpeed) < LadderAnimationEpsilon
            && Math.Abs(previous.AnimationProgress - state.AnimationProgress) < LadderAnimationEpsilon
            && previous.AnimationState == state.AnimationState
            && Math.Abs(previous.FallAngularSpeed - state.FallAngularSpeed) < LadderAnimationEpsilon
            && previous.AnimationIndex == state.AnimationIndex
            && LadderFramesEqual(previous.Frame, state.Frame);
    }

    private static bool LadderFramesEqual(MatrixFrame left, MatrixFrame right)
    {
        float thresholdSquared = LadderFrameEpsilon * LadderFrameEpsilon;
        return (left.origin - right.origin).LengthSquared < thresholdSquared
            && (left.rotation.s - right.rotation.s).LengthSquared < thresholdSquared
            && (left.rotation.f - right.rotation.f).LengthSquared < thresholdSquared
            && (left.rotation.u - right.rotation.u).LengthSquared < thresholdSquared;
    }

    // Stamp the current host epoch, sender and per-machine claim revision at the send boundary.
    // CaptureState stays a pure machine read; lastSent keeps the unstamped capture.
    private NetworkSiegeMachineState Stamp(NetworkSiegeMachineState state)
    {
        return new NetworkSiegeMachineState(state.MachineId, state.HitPoints, state.DestructionState,
            state.GateState, state.LadderState, state.MoveDistance, state.HasArrived, state.WeaponState,
            state.AimDirection, state.AimReleaseAngle, GetSnapshotAuthorityEpoch(state.MachineId),
            state.HasStoneAmmo ? state.StoneAmmo : -1,
            session.OwnControllerId,
            GetAuthorityRevision(state.MachineId));
    }

    private NetworkSiegeLadderAnimationState Stamp(NetworkSiegeLadderAnimationState state)
    {
        return new NetworkSiegeLadderAnimationState(
            state.LadderId,
            state.AnimationSpeed,
            state.AnimationProgress,
            state.AnimationState,
            state.FallAngularSpeed,
            state.Frame,
            state.AnimationIndex,
            GetSnapshotAuthorityEpoch(state.LadderId),
            session.OwnControllerId,
            GetAuthorityRevision(state.LadderId));
    }

    private int GetAuthorityRevision(int machineId)
        => authorityRevisions.TryGetValue(machineId, out var revision) ? revision : 0;

    private int GetAuthorityEpoch(int machineId)
        => authorityEpochs.TryGetValue(machineId, out var epoch) ? epoch : 0;

    private int GetSnapshotAuthorityEpoch(int machineId)
        => authorityEpochs.TryGetValue(machineId, out var epoch) ? epoch : session.HostEpoch;

    private string GetAuthorityHostController(int machineId)
        => authorityHostControllers.TryGetValue(machineId, out var controllerId)
            ? controllerId
            : string.Empty;

    public bool TryGetMachineAuthority(
        int machineId,
        out string controllerId,
        out int hostEpoch,
        out int authorityRevision)
    {
        // A promoted host can receive an old claimant event before the first poll has re-announced
        // inherited claims. Normalize the tuple before returning it so old-epoch events cannot pass
        // an authority check during that window.
        if (session.IsLocalHost && GetAuthorityEpoch(machineId) < session.HostEpoch)
        {
            NormalizeAuthorityForCurrentHost(machineId, initializeIfMissing: true);
        }

        if (claimedMachines.TryGetValue(machineId, out controllerId))
        {
            hostEpoch = GetSnapshotAuthorityEpoch(machineId);
            authorityRevision = GetAuthorityRevision(machineId);
            return true;
        }

        if (session.IsLocalHost)
        {
            controllerId = session.OwnControllerId;
            hostEpoch = session.HostEpoch;
            authorityRevision = GetAuthorityEpoch(machineId) == hostEpoch
                ? GetAuthorityRevision(machineId)
                : 0;
            return !string.IsNullOrEmpty(controllerId);
        }

        controllerId = GetAuthorityHostController(machineId);
        hostEpoch = GetSnapshotAuthorityEpoch(machineId);
        authorityRevision = GetAuthorityRevision(machineId);
        return !string.IsNullOrEmpty(controllerId);
    }

    // BR-102: drop a host-authority message stamped by an earlier hosting generation (a deposed host
    // in flight across a migration). Unstamped (0) senders, an unassigned (0) receiver, and epochs at
    // or ahead of our assignment are accepted — see HostEpochPolicy for the convergence rationale.
    private bool DropStaleHostEpoch(int messageEpoch, string messageName)
    {
        int localEpoch = session.HostEpoch;
        if (!hostEpochPolicy.IsStale(messageEpoch, localEpoch)) return false;

        Logger.Information("[BattleSync] Dropped {Message} stamped with stale host epoch {Stale} (current {Current})",
            messageName, messageEpoch, localEpoch);
        return true;
    }

    // Capture exactly the fields this simulator owns. The same routine is used for steady-state deltas and
    // on-demand join snapshots so a stable machine does not depend on having changed since the last join.
    private static NetworkSiegeMachineState CaptureState(UsableMachine machine, bool isHost, bool simulatedLocally)
    {
        ReadState(machine, out var hitPoints, out var destructionState, out var gateState, out var ladderState,
            out var moveDistance, out var hasArrived, out var weaponState, out var aimDirection,
            out var aimReleaseAngle, out var stoneAmmo);

        if (isHost && !simulatedLocally)
        {
            // The claiming peer reports this machine's simulator-owned fields; the host stays
            // authoritative for damage. Sentinels let receivers merge both snapshots.
            weaponState = -1;
            aimDirection = AimSentinel;
            aimReleaseAngle = AimSentinel;
            moveDistance = -1f;
            hasArrived = false;
            if (machine is CastleGate || machine is SiegeTower) gateState = -1;
            if (machine is SiegeLadder) ladderState = -1;
            if (machine is StonePile) stoneAmmo = -1;
        }
        else if (!isHost)
        {
            hitPoints = -1f;
            destructionState = -1;
            if (!simulatedLocally)
            {
                gateState = -1;
                ladderState = -1;
                moveDistance = -1f;
                hasArrived = false;
                weaponState = -1;
                aimDirection = AimSentinel;
                aimReleaseAngle = AimSentinel;
                stoneAmmo = -1;
            }
        }

        return new NetworkSiegeMachineState(machine.Id.Id, hitPoints, destructionState, gateState, ladderState,
            moveDistance, hasArrived, weaponState, aimDirection, aimReleaseAngle, stoneAmmo: stoneAmmo);
    }

    private static NetworkSiegeLadderAnimationState CaptureLadderAnimationState(SiegeLadder ladder)
    {
        int animationIndex = ladder._ladderSkeleton.GetAnimationIndexAtChannel(0);
        float animationSpeed = LadderAnimationSentinel;
        float animationProgress = LadderAnimationSentinel;
        if (animationIndex >= 0)
        {
            animationSpeed = ladder._ladderSkeleton.GetAnimationSpeedAtChannel(0);
            animationProgress = ladder._ladderSkeleton.GetAnimationParameterAtChannel(0);
        }

        return new NetworkSiegeLadderAnimationState(
            ladder.Id.Id,
            animationSpeed,
            animationProgress,
            (int)ladder._animationState,
            ladder._fallAngularSpeed,
            ladder._ladderObject.GameEntity.GetGlobalFrame(),
            animationIndex);
    }

    private void DeactivateNewMachines()
    {
        foreach (var machine in machines)
        {
            // Keep primary weapons live for local tactics, and keep player interactables live so
            // vanilla can focus and highlight their standing points on every peer.
            if (machine is IPrimarySiegeWeapon || machine is RangedSiegeWeapon
                || machine is CastleGate || machine is StonePile
                || machine is AmmoBarrelBase || machine is SiegeMachineStonePile)
            {
                continue;
            }

            if (!deactivated.Add(machine.Id.Id)) continue;

            machine.Deactivate();
        }
    }

    private static bool IsMovementMachine(UsableMachine machine)
    {
        return machine is BatteringRam || machine is SiegeTower;
    }

    private static bool IsGrantableMachine(UsableMachine machine)
    {
        return machine is RangedSiegeWeapon || IsMovementMachine(machine)
            || machine is SiegeLadder || machine is CastleGate || machine is StonePile;
    }

    private static bool IsCrewGrantableMachine(UsableMachine machine)
    {
        return machine is RangedSiegeWeapon || IsMovementMachine(machine);
    }

    // Keep every grantable machine's local crew in line with its simulation owner: the owner's
    // simulation crews it, everyone else's AI stays off non-primary machines, and losing a primary
    // sim vacates the local crew. Disabling a primary weapon crashes the attacker tactic.
    private void RefreshMachineGates()
    {
        foreach (var machine in machines)
        {
            if (!IsGrantableMachine(machine)) continue;

            int machineId = machine.Id.Id;
            bool simulatedLocally = SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machineId);

            if (!(machine is IPrimarySiegeWeapon))
            {
                bool disableAi = !simulatedLocally;
                if (disableAi == aiDisabledMachines.Contains(machineId)) continue;

                machine.SetIsDisabledForAI(disableAi);
                if (disableAi)
                {
                    aiDisabledMachines.Add(machineId);
                    VacateLocalUsers(machine);
                }
                else
                {
                    aiDisabledMachines.Remove(machineId);
                }
            }
            else if (simulatedLocally)
            {
                locallySimulatedPrimaryMachines.Add(machineId);
            }
            else if (locallySimulatedPrimaryMachines.Remove(machineId))
            {
                VacateLocalUsers(machine);
            }
        }
    }

    // [Host] Hand an idle machine's simulation to the client whose troops are its natural crew,
    // instead of walking our own across the map. The winner must hold for a few polls, and a
    // machine with a seated crew is left alone.
    private void EvaluateProximityCrewGrants(float elapsed)
    {
        crewSnapshotValid = false;
        foreach (var machine in machines)
        {
            if (!(machine is SiegeWeapon siegeWeapon) || !IsCrewGrantableMachine(machine)) continue;

            int machineId = machine.Id.Id;

            if (regrantCooldown.TryGetValue(machineId, out var cooldown))
            {
                cooldown -= elapsed;
                if (cooldown > 0f)
                {
                    regrantCooldown[machineId] = cooldown;
                    continue;
                }

                regrantCooldown.Remove(machineId);
            }

            if (claimedMachines.ContainsKey(machineId))
            {
                ClearGrantStreak(machineId);
                // Our own mounted player outranks a crew grant (never a mount claim); so does the
                // machine dying under the grantee.
                if (proximityGrants.Contains(machineId)
                    && (HasLocalPlayerUser(machine) || machine.DestructionComponent?.IsDestroyed == true))
                {
                    proximityGrants.Remove(machineId);
                    SetMachineAuthority(machineId, null);
                }

                continue;
            }

            if (machine.IsDeactivated || machine.DestructionComponent?.IsDestroyed == true || HasSeatedUser(machine))
            {
                ClearGrantStreak(machineId);
                continue;
            }

            if (machine is RangedSiegeWeapon rangedWeapon)
            {
                if (!rangedWeapon.HasAmmo)
                {
                    ClearGrantStreak(machineId);
                    continue;
                }

                // A contested machine (enemies within its stop-using range) blocks seats and kicks
                // crews on every client — granting it now only starts a grant/release flap while
                // the machine sits dead; wait for the melee to clear.
                if (rangedWeapon.IsDisabledForBattleSideAI(rangedWeapon.Side))
                {
                    ClearGrantStreak(machineId);
                    continue;
                }
            }

            // An arrived ram/tower no longer needs a pushing crew; leave its sim where it is.
            if ((machine is BatteringRam arrivedRam && arrivedRam.HasArrivedAtTarget)
                || (machine is SiegeTower arrivedTower && arrivedTower.HasArrivedAtTarget))
            {
                ClearGrantStreak(machineId);
                continue;
            }

            var winner = FindDominantCrewController(siegeWeapon);
            if (string.IsNullOrEmpty(winner) || winner == session.OwnControllerId)
            {
                ClearGrantStreak(machineId);
                continue;
            }

            if (grantWinner.TryGetValue(machineId, out var previous) && previous == winner)
            {
                if (grantStreak[machineId] + 1 >= GrantStreakRequired)
                {
                    ClearGrantStreak(machineId);
                    proximityGrants.Add(machineId);
                    Logger.Information("[BattleSync] Siege machine {Machine} crew-granted to {Controller}", machineId, winner);
                    SetMachineAuthority(machineId, winner);
                }
                else
                {
                    grantStreak[machineId] += 1;
                }
            }
            else
            {
                grantWinner[machineId] = winner;
                grantStreak[machineId] = 1;
            }
        }
    }

    private void ClearGrantStreak(int machineId)
    {
        grantWinner.Remove(machineId);
        grantStreak.Remove(machineId);
    }

    private static bool HasSeatedUser(UsableMachine machine)
    {
        foreach (var standingPoint in machine.StandingPoints)
        {
            if (standingPoint.UserAgent != null) return true;
        }

        return false;
    }

    // The controller with the most same-side human agents within CrewSearchRadius; ties go to us.
    // An attacker-side machine with no decisive nearby crew falls back to a mission-wide count: its
    // crew cannot walk over BEFORE the grant (the manning scan only runs on the simulator), and a
    // defender host has no attacker agents of its own at all.
    private string FindDominantCrewController(SiegeWeapon machine)
    {
        if (!crewSnapshotValid)
        {
            BuildCrewSnapshot();
            crewSnapshotValid = true;
        }

        var machinePosition = machine.GameEntity.GlobalPosition;
        bool missionWideFallback = machine.Side == BattleSideEnum.Attacker;
        Dictionary<string, int> nearCounts = null;
        Dictionary<string, int> totalCounts = null;
        foreach (var candidate in crewCandidates)
        {
            if (candidate.Side != machine.Side) continue;

            if (missionWideFallback)
            {
                if (totalCounts == null) totalCounts = new Dictionary<string, int>();
                totalCounts.TryGetValue(candidate.Controller, out var total);
                totalCounts[candidate.Controller] = total + 1;
            }

            if ((candidate.Position - machinePosition).LengthSquared > CrewSearchRadius * CrewSearchRadius) continue;

            if (nearCounts == null) nearCounts = new Dictionary<string, int>();
            nearCounts.TryGetValue(candidate.Controller, out var near);
            nearCounts[candidate.Controller] = near + 1;
        }

        // A real nearby crew (ours included) outranks the fallback, so with two attacker players a
        // machine goes to whoever is standing at it, not the bigger army across the map.
        var nearWinner = PickCrewWinner(nearCounts);
        if (nearWinner != null && (nearWinner != session.OwnControllerId || HasOwnCount(nearCounts))) return nearWinner;

        return missionWideFallback ? PickCrewWinner(totalCounts) : nearWinner;
    }

    private bool HasOwnCount(Dictionary<string, int> counts)
    {
        return counts != null && counts.TryGetValue(session.OwnControllerId, out var own) && own > 0;
    }

    private string PickCrewWinner(Dictionary<string, int> counts)
    {
        if (counts == null) return null;

        string winner = null;
        int best = 0;
        foreach (var candidate in counts)
        {
            if (candidate.Value > best)
            {
                best = candidate.Value;
                winner = candidate.Key;
            }
        }

        counts.TryGetValue(session.OwnControllerId, out var ownCount);
        if (winner != session.OwnControllerId && (best < 2 || ownCount >= best)) return session.OwnControllerId;

        return winner;
    }

    private void BuildCrewSnapshot()
    {
        crewCandidates.Clear();
        foreach (var agent in Mission.Current.Agents)
        {
            if (!agent.IsHuman || !agent.IsActive() || agent.IsRunningAway) continue;
            if (agent.Team == null) continue;

            var controller = agentRegistry.TryGetAgentInfo(agent, out var info)
                ? info.CurrentAuthority
                : session.OwnControllerId;
            if (string.IsNullOrEmpty(controller)) continue;

            crewCandidates.Add(new CrewCandidate { Controller = controller, Side = agent.Team.Side, Position = agent.Position });
        }
    }

    private struct CrewCandidate
    {
        public string Controller;
        public BattleSideEnum Side;
        public TaleWorlds.Library.Vec3 Position;
    }

    // [Peer] Claim a machine when our player mounts it; release it back to the host once nothing
    // of ours has used it for a while.
    private void ScanMachineClaims(float elapsed)
    {
        foreach (var machine in machines)
        {
            if (!IsGrantableMachine(machine)) continue;

            int machineId = machine.Id.Id;
            if (SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machineId))
            {
                if (HasLocalUserOrMover(machine))
                {
                    unusedOwnedSeconds.Remove(machineId);
                    grantGrace.Remove(machineId);
                    continue;
                }

                // A contested machine can't be crewed by anyone; hold the clocks instead of
                // handing it back and burning the regrant cooldown.
                if (machine is SiegeWeapon contested && contested.IsDisabledForBattleSideAI(contested.Side)) continue;

                // Vanilla's crewing pipeline cold-starts after a grant (detachment score warm-up,
                // navmesh filters) and can run out the release clock without seating anyone; seat
                // our own nearest troops through vanilla's own assignment call instead.
                if (machine is RangedSiegeWeapon) TrySeatOwnCrew(machine);

                // A fresh crew grant gets a grace window for our troops to walk over.
                if (grantGrace.TryGetValue(machineId, out var grace))
                {
                    grace -= elapsed;
                    if (grace > 0f)
                    {
                        grantGrace[machineId] = grace;
                        continue;
                    }

                    grantGrace.Remove(machineId);
                }

                unusedOwnedSeconds.TryGetValue(machineId, out var unused);
                unused += elapsed;
                if (unused >= ReleaseAfterUnusedSeconds)
                {
                    unusedOwnedSeconds.Remove(machineId);
                    network.SendAll(new NetworkSiegeMachineClaim(machineId, session.OwnControllerId, isRelease: true));
                }
                else
                {
                    unusedOwnedSeconds[machineId] = unused;
                }
            }
            else
            {
                if (!HasLocalPlayerUser(machine))
                {
                    pendingClaimSeconds.Remove(machineId);
                    continue;
                }

                if (pendingClaimSeconds.TryGetValue(machineId, out var sinceRequest))
                {
                    sinceRequest += elapsed;
                    if (sinceRequest < ClaimRetrySeconds)
                    {
                        pendingClaimSeconds[machineId] = sinceRequest;
                        continue;
                    }
                }

                pendingClaimSeconds[machineId] = 0f;
                network.SendAll(new NetworkSiegeMachineClaim(machineId, session.OwnControllerId, isRelease: false));
            }
        }
    }

    // [Grantee, game thread] Seat our own nearest eligible troops on the granted machine's empty
    // standing points, using vanilla's own assignment call. The vanilla pipeline needs its
    // detachment score table warmed up and passes navmesh/detachment filters that can starve a
    // freshly-granted machine past the release clock.
    private void TrySeatOwnCrew(UsableMachine machine)
    {
        var mission = Mission.Current;
        for (int slotIndex = 0; slotIndex < machine.StandingPoints.Count; slotIndex++)
        {
            var standingPoint = machine.StandingPoints[slotIndex];
            if (standingPoint.HasUser || standingPoint.HasAIMovingTo || standingPoint.IsDeactivated) continue;

            Agent best = null;
            float bestDistance = CrewSearchRadius * CrewSearchRadius;
            foreach (var agent in mission.Agents)
            {
                if (!agent.IsHuman || !agent.IsActive() || !agent.IsAIControlled || agent.IsRunningAway) continue;
                if (agent.Detachment != null || !agentRegistry.IsLocallyControlled(agent)) continue;
                if (!agent.CanBeAssignedForScriptedMovement()) continue;
                if (standingPoint.IsDisabledForAgent(agent)) continue;

                float distance = (agent.Position - standingPoint.GameEntity.GlobalPosition).LengthSquared;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = agent;
                }
            }

            if (best == null) continue;

            // The assignment's detachment bookkeeping indexes the team's JOINED detachments and
            // throws when the agent's formation never joined the machine; join it first.
            best.Formation?.JoinDetachment(machine);
            machine.AddAgentAtSlotIndex(best, slotIndex);
        }
    }

    private static bool HasLocalPlayerUser(UsableMachine machine)
    {
        foreach (var standingPoint in machine.StandingPoints)
        {
            if (standingPoint.UserAgent?.IsMine == true) return true;
        }

        return false;
    }

    private bool HasLocalUserOrMover(UsableMachine machine)
    {
        foreach (var standingPoint in machine.StandingPoints)
        {
            var user = standingPoint.UserAgent;
            if (user != null && agentRegistry.IsLocallyControlled(user)) return true;

            var mover = standingPoint.MovingAgent;
            if (mover != null && agentRegistry.IsLocallyControlled(mover)) return true;
        }

        return false;
    }

    // [Game thread] Kick our locally-simulated crew off a machine whose sim moved elsewhere — the new
    // owner mans it with its own agents. The local player agent is left alone (never yank the avatar);
    // the replicated state overrides whatever it does on a machine it doesn't own.
    private void VacateLocalUsers(UsableMachine machine)
    {
        foreach (var standingPoint in machine.StandingPoints)
        {
            var user = standingPoint.UserAgent;
            if (user != null && !user.IsMine && agentRegistry.IsLocallyControlled(user))
            {
                user.StopUsingGameObject(false);
            }
        }

        if (machine is SiegeWeapon siegeWeapon && siegeWeapon._forcedUseFormations != null && siegeWeapon._forcedUseFormations.Count > 0)
        {
            foreach (var formation in new List<Formation>(siegeWeapon._forcedUseFormations))
            {
                formation.StopUsingMachine(machine, !formation.IsAIControlled);
            }

            siegeWeapon._forcedUseFormations.Clear();
        }
    }

    // [Host] Arbitrate a peer's claim: first come keeps it, our own mounted player outranks it, and a
    // release hands the machine back. Every decision is (re-)announced so the requester stops waiting.
    private void Handle_NetworkMachineClaim(MessagePayload<NetworkSiegeMachineClaim> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null || !session.IsLocalHost) return;

            RefreshMachineCache();
            NormalizeAuthorityForCurrentHost(obj.MachineId, initializeIfMissing: false);
            if (GetAuthorityEpoch(obj.MachineId) > session.HostEpoch) return;
            if (obj.IsRelease)
            {
                if (claimedMachines.TryGetValue(obj.MachineId, out var owner) && owner == obj.ControllerId)
                {
                    // An unused hand-back of a crew grant blocks an immediate identical re-grant.
                    if (proximityGrants.Remove(obj.MachineId))
                    {
                        regrantCooldown[obj.MachineId] = RegrantCooldownSeconds;
                    }

                    SetMachineAuthority(obj.MachineId, null);
                }
                return;
            }

            if (claimedMachines.TryGetValue(obj.MachineId, out var current))
            {
                if (current == obj.ControllerId)
                {
                    // The grantee's own player mounted: the crew grant becomes a mount claim.
                    proximityGrants.Remove(obj.MachineId);
                    return;
                }

                // A mount claim outranks a crew-proximity grant, never another mount claim.
                if (proximityGrants.Remove(obj.MachineId))
                {
                    Logger.Information("[BattleSync] Siege machine {Machine} mount-claimed by {Controller}, preempting the crew grant", obj.MachineId, obj.ControllerId);
                    SetMachineAuthority(obj.MachineId, obj.ControllerId);
                    return;
                }

                network.SendAll(new NetworkSiegeMachineAuthority(
                    obj.MachineId,
                    current,
                    GetSnapshotAuthorityEpoch(obj.MachineId),
                    GetAuthorityRevision(obj.MachineId),
                    session.OwnControllerId));
                return;
            }

            if (machinesById.TryGetValue(obj.MachineId, out var machine) && HasLocalPlayerUser(machine))
            {
                network.SendAll(new NetworkSiegeMachineAuthority(
                    obj.MachineId,
                    string.Empty,
                    GetSnapshotAuthorityEpoch(obj.MachineId),
                    GetAuthorityRevision(obj.MachineId),
                    session.OwnControllerId));
                return;
            }

            Logger.Information("[BattleSync] Siege machine {Machine} claimed by {Controller}", obj.MachineId, obj.ControllerId);
            SetMachineAuthority(obj.MachineId, obj.ControllerId);
        });
    }

    // [Host, game thread] Record and announce a machine's simulation owner (null = back to the host).
    private void SetMachineAuthority(int machineId, string controllerId)
    {
        NormalizeAuthorityForCurrentHost(machineId, initializeIfMissing: false);
        int hostEpoch = session.HostEpoch;
        if (GetAuthorityEpoch(machineId) > hostEpoch) return;
        int authorityRevision = GetAuthorityEpoch(machineId) == hostEpoch
            ? GetAuthorityRevision(machineId) + 1
            : 1;
        authorityEpochs[machineId] = hostEpoch;
        authorityRevisions[machineId] = authorityRevision;
        authorityHostControllers[machineId] = session.OwnControllerId;
        if (string.IsNullOrEmpty(controllerId))
        {
            claimedMachines.Remove(machineId);
        }
        else
        {
            claimedMachines[machineId] = controllerId;
        }

        PushClaimsToGate();
        // Every authority revision needs a freshly stamped host-owned snapshot, including a
        // remote claimant replacing another remote claimant while the damage fields stay unchanged.
        lastSent.Remove(machineId);
        lastSentLadderAnimations.Remove(machineId);
        // BR-102: the arbitration decision is THE host-authority act here — stamp our hosting generation.
        network.SendAll(new NetworkSiegeMachineAuthority(
            machineId,
            controllerId ?? string.Empty,
            hostEpoch,
            authorityRevision,
            session.OwnControllerId));
        AuthorityChanged?.Invoke(machineId);
        RefreshMachineGates();
    }

    private void Handle_NetworkMachineAuthority(MessagePayload<NetworkSiegeMachineAuthority> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            if (session.IsLocalHost && obj.HostEpoch <= session.HostEpoch) return;

            // BR-102: a deposed host's in-flight arbitration must not move a machine's simulation.
            if (DropStaleHostEpoch(obj.HostEpoch, nameof(NetworkSiegeMachineAuthority))) return;

            // Refresh BEFORE mutating, like every sibling handler: on a freshly-opened mission the
            // refresh takes the mission-change clear branch, which would wipe the claim just written.
            RefreshMachineCache();
            int authorityOrder = CompareAuthorityOrder(
                obj.HostEpoch,
                obj.AuthorityRevision,
                GetAuthorityEpoch(obj.MachineId),
                GetAuthorityRevision(obj.MachineId));
            if (authorityOrder < 0) return;

            if (authorityRevisions.ContainsKey(obj.MachineId)
                && authorityOrder == 0
                && (CurrentAuthorityOwner(obj.MachineId) != (obj.ControllerId ?? string.Empty)
                    || GetAuthorityHostController(obj.MachineId) != (obj.SenderControllerId ?? string.Empty)))
            {
                return;
            }

            bool wasSimulatedLocally = SiegeMissionAuthorityGate.IsMachineSimulatedLocally(obj.MachineId);

            if (string.IsNullOrEmpty(obj.ControllerId))
            {
                claimedMachines.Remove(obj.MachineId);
            }
            else
            {
                claimedMachines[obj.MachineId] = obj.ControllerId;
            }
            authorityEpochs[obj.MachineId] = obj.HostEpoch;
            authorityRevisions[obj.MachineId] = obj.AuthorityRevision;
            authorityHostControllers[obj.MachineId] = obj.SenderControllerId ?? string.Empty;

            // An unsolicited self-grant is a crew-proximity grant: give our troops time to walk over
            // before the unused-release clock starts.
            if (obj.ControllerId == session.OwnControllerId && !pendingClaimSeconds.ContainsKey(obj.MachineId))
            {
                grantGrace[obj.MachineId] = GrantGraceSeconds;
            }

            pendingClaimSeconds.Remove(obj.MachineId);
            PushClaimsToGate();
            if (authorityOrder > 0 && SiegeMissionAuthorityGate.IsMachineSimulatedLocally(obj.MachineId))
            {
                lastSent.Remove(obj.MachineId);
                lastSentLadderAnimations.Remove(obj.MachineId);
            }
            else
            {
                ResetSendCacheWhenSimulationMovesHere(obj.MachineId, wasSimulatedLocally);
            }
            AuthorityChanged?.Invoke(obj.MachineId);
            RefreshMachineGates();
            DrainPendingMachineStates();
        });
    }

    private string CurrentAuthorityOwner(int machineId)
        => claimedMachines.TryGetValue(machineId, out var owner) ? owner : string.Empty;

    private static int CompareAuthorityOrder(
        int messageEpoch,
        int messageRevision,
        int currentEpoch,
        int currentRevision)
    {
        if (messageEpoch != currentEpoch) return messageEpoch.CompareTo(currentEpoch);

        return messageRevision.CompareTo(currentRevision);
    }

    private void ResetSendCacheWhenSimulationMovesHere(int machineId, bool wasSimulatedLocally)
    {
        if (wasSimulatedLocally || !SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machineId)) return;

        lastSent.Remove(machineId);
        lastSentLadderAnimations.Remove(machineId);
    }

    private void PushClaimsToGate()
    {
        var locallyClaimed = new HashSet<int>();
        var remotelyClaimed = new HashSet<int>();
        foreach (var claim in claimedMachines)
        {
            if (claim.Value == session.OwnControllerId)
            {
                locallyClaimed.Add(claim.Key);
            }
            else
            {
                remotelyClaimed.Add(claim.Key);
            }
        }

        SiegeMissionAuthorityGate.SetClaimedMachines(locallyClaimed, remotelyClaimed);
    }

    private void Handle_MissionPeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;
        ReclaimMachinesOf(payload.What.ControllerId);
    }

    private void Handle_MissionPeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;
        ReclaimMachinesOf(payload.What.ControllerId);
    }

    // [Host] A departed controller's claims return to the host so its machines keep simulating.
    private void ReclaimMachinesOf(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null || !session.IsLocalHost || claimedMachines.Count == 0) return;

            List<int> reclaimed = null;
            foreach (var claim in claimedMachines)
            {
                if (claim.Value != controllerId) continue;
                if (reclaimed == null) reclaimed = new List<int>();
                reclaimed.Add(claim.Key);
            }

            if (reclaimed == null) return;

            RefreshMachineCache();
            foreach (var machineId in reclaimed)
            {
                proximityGrants.Remove(machineId);
                SetMachineAuthority(machineId, null);
            }

            Logger.Information("[BattleSync] Reclaimed {Count} siege machine(s) from departed {Controller}", reclaimed.Count, controllerId);
        });
    }

    // Re-apply buffered states whose MissionObject has now registered. Runs on every client each poll; the
    // buffer only fills from received states, so a client that receives none skips it.
    private void DrainPendingMachineStates()
    {
        DrainPendingGeneralMachineStates();
        DrainPendingLadderAnimationStates();
    }

    private void DrainPendingGeneralMachineStates()
    {
        List<int> applied = null;
        foreach (var pending in pendingByMachineId)
        {
            if (DropStaleHostEpoch(pending.Value.HostEpoch, nameof(NetworkSiegeMachineState)))
            {
                if (applied == null) applied = new List<int>();
                applied.Add(pending.Key);
                continue;
            }
            var authority = ClassifySnapshotAuthority(
                pending.Key,
                pending.Value.HostEpoch,
                pending.Value.AuthorityRevision,
                pending.Value.SenderControllerId);
            if (authority == SnapshotAuthority.Buffer) continue;
            if (authority == SnapshotAuthority.Drop)
            {
                if (applied == null) applied = new List<int>();
                applied.Add(pending.Key);
                continue;
            }
            if (!machinesById.TryGetValue(pending.Key, out var machine)) continue;

            SiegeMissionAuthorityGate.SuppressCapture = true;
            try
            {
                Apply(machine, pending.Value);
            }
            finally
            {
                SiegeMissionAuthorityGate.SuppressCapture = false;
            }

            if (!SiegeMissionAuthorityGate.IsMachineSimulatedLocally(pending.Key))
            {
                if (pending.Value.AimDirection > AimSentinel + 1f)
                {
                    SiegeMissionAuthorityGate.SetRemoteAim(pending.Key, pending.Value.AimDirection, pending.Value.AimReleaseAngle);
                }

                ApplyWeaponAnimation(machine, pending.Value.WeaponState);
            }

            if (applied == null) applied = new List<int>();
            applied.Add(pending.Key);
        }

        if (applied == null) return;
        foreach (var id in applied) pendingByMachineId.Remove(id);
    }

    private void DrainPendingLadderAnimationStates()
    {
        List<int> applied = null;
        foreach (var pending in pendingLadderAnimationsById)
        {
            if (DropStaleHostEpoch(pending.Value.HostEpoch, nameof(NetworkSiegeLadderAnimationState)))
            {
                if (applied == null) applied = new List<int>();
                applied.Add(pending.Key);
                continue;
            }
            var authority = ClassifySnapshotAuthority(
                pending.Key,
                pending.Value.HostEpoch,
                pending.Value.AuthorityRevision,
                pending.Value.SenderControllerId);
            if (authority == SnapshotAuthority.Buffer) continue;
            if (authority == SnapshotAuthority.Drop)
            {
                if (applied == null) applied = new List<int>();
                applied.Add(pending.Key);
                continue;
            }
            if (!machinesById.TryGetValue(pending.Key, out var machine)) continue;

            if (machine is SiegeLadder ladder
                && !SiegeMissionAuthorityGate.IsMachineSimulatedLocally(pending.Key))
            {
                ApplyLadderAnimation(ladder, pending.Value);
            }
            if (applied == null) applied = new List<int>();
            applied.Add(pending.Key);
        }

        if (applied == null) return;
        foreach (var id in applied) pendingLadderAnimationsById.Remove(id);
    }

    private static void ReadState(UsableMachine machine, out float hitPoints, out int destructionState,
        out int gateState, out int ladderState, out float moveDistance, out bool hasArrived, out int weaponState,
        out float aimDirection, out float aimReleaseAngle, out int stoneAmmo)
    {
        hitPoints = -1f;
        destructionState = -1;
        if (machine.DestructionComponent != null)
        {
            hitPoints = machine.DestructionComponent.HitPoint;
            destructionState = machine.DestructionComponent._currentStateIndex;
        }

        // A siege tower's ramp is its own gate state (vanilla replicates it with a dedicated
        // network message); it rides the same field as the castle gate's.
        gateState = machine is CastleGate gate ? (int)gate.State
            : machine is SiegeTower gateTower ? (int)gateTower.State : -1;
        ladderState = machine is SiegeLadder ladder ? (int)ladder.State : -1;
        stoneAmmo = machine is StonePile stonePile ? stonePile.AmmoCount : -1;
        weaponState = -1;
        aimDirection = AimSentinel;
        aimReleaseAngle = AimSentinel;
        if (machine is RangedSiegeWeapon rangedWeapon)
        {
            weaponState = (int)rangedWeapon.State;
            // Send the targets, not the current angles: they settle when idle, and the receiver
            // replays the same speed-limited approach so the body turns smoothly.
            aimDirection = rangedWeapon.TargetDirection;
            aimReleaseAngle = rangedWeapon.TargetReleaseAngle;
        }

        moveDistance = -1f;
        hasArrived = false;
        if (machine is BatteringRam ram)
        {
            moveDistance = ram.MovementComponent._pathTracker.TotalDistanceTraveled;
            hasArrived = ram.HasArrivedAtTarget;
        }
        else if (machine is SiegeTower tower)
        {
            moveDistance = tower.MovementComponent._pathTracker.TotalDistanceTraveled;
            hasArrived = tower.HasArrivedAtTarget;
        }
    }

    private void Handle_NetworkMachineState(MessagePayload<NetworkSiegeMachineState> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            // BR-102: a deposed host's snapshot (its host-owned fields carry damage and removal-relevant
            // state) must not fight the promoted host's simulation — nor even be buffered for re-apply.
            if (DropStaleHostEpoch(obj.HostEpoch, nameof(NetworkSiegeMachineState))) return;

            RefreshMachineCache();
            var authority = ClassifySnapshotAuthority(
                obj.MachineId,
                obj.HostEpoch,
                obj.AuthorityRevision,
                obj.SenderControllerId);
            if (authority == SnapshotAuthority.Drop) return;
            if (authority == SnapshotAuthority.Buffer)
            {
                BufferMachineState(obj);
                return;
            }
            if (!machinesById.TryGetValue(obj.MachineId, out var machine))
            {
                // A net-zero MissionObjects churn (an item spawned and despawned within a poll) can hide
                // a newly-added machine from the count-gated cache; force a list rebuild before giving up.
                // Only the count is poked: nulling trackedMission takes the mission-change branch, which
                // wipes every claim and buffered state.
                trackedObjectCount = -1;
                RefreshMachineCache();
                if (!machinesById.TryGetValue(obj.MachineId, out machine))
                {
                    // The MissionObject isn't registered yet (catch-up during scene load); buffer and re-apply
                    // from Tick once RefreshMachineCache sees it, instead of dropping it permanently.
                    BufferMachineState(obj);
                    return;
                }
            }

            var stateToApply = ReconcilePendingMachineStateForLiveApply(obj);
            SiegeMissionAuthorityGate.SuppressCapture = true;
            try
            {
                Apply(machine, stateToApply);
            }
            finally
            {
                SiegeMissionAuthorityGate.SuppressCapture = false;
            }

            // A machine we simulate runs its own weapon state machine; a stale pre-claim broadcast
            // must not drive its arm animation or aim.
            if (!SiegeMissionAuthorityGate.IsMachineSimulatedLocally(obj.MachineId))
            {
                if (stateToApply.AimDirection > AimSentinel + 1f)
                {
                    SiegeMissionAuthorityGate.SetRemoteAim(
                        obj.MachineId,
                        stateToApply.AimDirection,
                        stateToApply.AimReleaseAngle);
                }

                ApplyWeaponAnimation(machine, stateToApply.WeaponState);
            }
        });
    }

    private void Handle_NetworkLadderAnimationState(MessagePayload<NetworkSiegeLadderAnimationState> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            if (DropStaleHostEpoch(obj.HostEpoch, nameof(NetworkSiegeLadderAnimationState))) return;

            RefreshMachineCache();
            var authority = ClassifySnapshotAuthority(
                obj.LadderId,
                obj.HostEpoch,
                obj.AuthorityRevision,
                obj.SenderControllerId);
            if (authority == SnapshotAuthority.Drop) return;
            if (authority == SnapshotAuthority.Buffer)
            {
                BufferLadderAnimationState(obj);
                return;
            }
            if (!machinesById.TryGetValue(obj.LadderId, out var machine))
            {
                trackedObjectCount = -1;
                RefreshMachineCache();
                if (!machinesById.TryGetValue(obj.LadderId, out machine))
                {
                    BufferLadderAnimationState(obj);
                    return;
                }
            }

            if (pendingByMachineId.ContainsKey(obj.LadderId))
            {
                BufferLadderAnimationState(obj);
                return;
            }

            pendingLadderAnimationsById.Remove(obj.LadderId);
            if (machine is SiegeLadder ladder
                && !SiegeMissionAuthorityGate.IsMachineSimulatedLocally(obj.LadderId))
            {
                ApplyLadderAnimation(ladder, obj);
            }
        });
    }

    private void BufferMachineState(NetworkSiegeMachineState state)
    {
        if (pendingByMachineId.TryGetValue(state.MachineId, out var existing))
        {
            int authorityOrder = CompareAuthorityOrder(
                existing.HostEpoch,
                existing.AuthorityRevision,
                state.HostEpoch,
                state.AuthorityRevision);
            if (authorityOrder > 0) return;
            if (authorityOrder == 0)
            {
                // A claimed machine's host and simulator send complementary fields at the same tuple.
                pendingByMachineId[state.MachineId] = MergeMachineStates(existing, state);
                return;
            }
        }

        pendingByMachineId[state.MachineId] = state;
    }

    private NetworkSiegeMachineState ReconcilePendingMachineStateForLiveApply(
        NetworkSiegeMachineState incoming)
    {
        if (!pendingByMachineId.TryGetValue(incoming.MachineId, out var pending)) return incoming;

        int authorityOrder = CompareAuthorityOrder(
            pending.HostEpoch,
            pending.AuthorityRevision,
            incoming.HostEpoch,
            incoming.AuthorityRevision);
        if (authorityOrder > 0) return incoming;

        pendingByMachineId.Remove(incoming.MachineId);
        return authorityOrder == 0 ? MergeMachineStates(pending, incoming) : incoming;
    }

    private static NetworkSiegeMachineState MergeMachineStates(
        NetworkSiegeMachineState existing,
        NetworkSiegeMachineState incoming)
    {
        bool incomingHasAim = incoming.AimDirection > AimSentinel + 1f;
        bool incomingHasMovement = incoming.MoveDistance >= 0f;
        return new NetworkSiegeMachineState(
            incoming.MachineId,
            incoming.HitPoints >= 0f ? incoming.HitPoints : existing.HitPoints,
            incoming.DestructionState >= 0 ? incoming.DestructionState : existing.DestructionState,
            incoming.GateState >= 0 ? incoming.GateState : existing.GateState,
            incoming.LadderState >= 0 ? incoming.LadderState : existing.LadderState,
            incomingHasMovement ? incoming.MoveDistance : existing.MoveDistance,
            incomingHasMovement ? incoming.HasArrived : existing.HasArrived,
            incoming.WeaponState >= 0 ? incoming.WeaponState : existing.WeaponState,
            incomingHasAim ? incoming.AimDirection : existing.AimDirection,
            incomingHasAim ? incoming.AimReleaseAngle : existing.AimReleaseAngle,
            incoming.HostEpoch,
            incoming.HasStoneAmmo
                ? incoming.StoneAmmo
                : (existing.HasStoneAmmo ? existing.StoneAmmo : -1),
            incoming.SenderControllerId,
            incoming.AuthorityRevision);
    }

    private void BufferLadderAnimationState(NetworkSiegeLadderAnimationState state)
    {
        if (pendingLadderAnimationsById.TryGetValue(state.LadderId, out var existing)
            && CompareAuthorityOrder(
                existing.HostEpoch,
                existing.AuthorityRevision,
                state.HostEpoch,
                state.AuthorityRevision) > 0)
        {
            return;
        }

        pendingLadderAnimationsById[state.LadderId] = state;
    }

    private SnapshotAuthority ClassifySnapshotAuthority(
        int machineId,
        int hostEpoch,
        int authorityRevision,
        string senderControllerId)
    {
        if (!authorityRevisions.ContainsKey(machineId)
            && authorityRevision == 0
            && session.IsHostController(senderControllerId))
        {
            return SnapshotAuthority.Apply;
        }

        int authorityOrder = CompareAuthorityOrder(
            hostEpoch,
            authorityRevision,
            GetAuthorityEpoch(machineId),
            GetAuthorityRevision(machineId));
        if (authorityOrder < 0) return SnapshotAuthority.Drop;
        if (authorityOrder > 0) return SnapshotAuthority.Buffer;
        if (GetAuthorityHostController(machineId) == senderControllerId) return SnapshotAuthority.Apply;

        return claimedMachines.TryGetValue(machineId, out var owner)
            && owner == senderControllerId
            ? SnapshotAuthority.Apply
            : SnapshotAuthority.Drop;
    }

    private enum SnapshotAuthority
    {
        Apply,
        Buffer,
        Drop,
    }

    private static void Apply(UsableMachine machine, NetworkSiegeMachineState state)
    {
        // The movement simulator ignores movement echoes (a stale pre-grant broadcast must not
        // fight its own tracker); everyone else eases toward the received distance.
        if (state.MoveDistance >= 0f && !SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machine.Id.Id))
        {
            var movement = (machine as BatteringRam)?.MovementComponent ?? (machine as SiegeTower)?.MovementComponent;
            if (movement != null)
            {
                // Vanilla MP's steady-state client recipe (MissionNetworkComponent's siege machine movement
                // handler): feed the distance into the movement component's advancement error and let its own
                // per-frame tick ease toward it — wheels turn and the move sound plays. The hard snap is
                // vanilla's JOIN-TIME recipe (BatteringRam.OnAfterReadFromNetwork), kept for a big gap
                // (joiner catch-up) and for a real backward correction; small negative error is the easing's
                // own overshoot and gets left alone.
                float error = state.MoveDistance - movement.GetTotalDistanceTraveledForPathTracker();
                if (error > MoveDistanceSnapThreshold || error < -MoveDistanceThreshold)
                {
                    movement.SetTotalDistanceTraveledForPathTracker(state.MoveDistance);
                    movement.SetTargetFrameForPathTracker();
                }
                else if (error > 0f)
                {
                    movement.SetDistanceTraveledAsClient(state.MoveDistance);
                }

                // Distance first, then the arrival flag whose setter flips the navmesh; MoveToTargetAsClient
                // puts the tracker exactly at the path end so arrival-gated logic (ladders, gates) sees
                // HasReachedEnd, mirroring vanilla's client arrival.
                if (state.HasArrived)
                {
                    if (machine is BatteringRam ram && !ram.HasArrivedAtTarget) ram.HasArrivedAtTarget = true;
                    else if (machine is SiegeTower tower && !tower.HasArrivedAtTarget) tower.HasArrivedAtTarget = true;
                    movement.MoveToTargetAsClient();
                }
            }
        }

        if (state.GateState >= 0 && machine is CastleGate gate && (int)gate.State != state.GateState)
        {
            if ((CastleGate.GateState)state.GateState == CastleGate.GateState.Open)
            {
                gate.OpenDoor();
            }
            else
            {
                gate.CloseDoor();
            }
        }

        // The tower ramp: assigning State is vanilla's own client apply (its network handler does
        // exactly this); the setter runs the fall animation, sound and navmesh flip.
        if (state.GateState >= 0 && machine is SiegeTower gateTower && (int)gateTower.State != state.GateState)
        {
            gateTower.State = (SiegeTower.GateState)state.GateState;
        }

        if (state.LadderState >= 0 && machine is SiegeLadder ladder)
        {
            var targetState = (SiegeLadder.LadderState)state.LadderState;
            if (ladder.State != targetState)
            {
                // Poll snapshots can skip vanilla's synchronous transition states that start or finish animations.
                foreach (var transitionState in GetLadderTransitionStates(ladder.State, targetState))
                {
                    ladder.State = transitionState;
                }
                ladder.State = targetState;
            }
        }

        if (state.HasStoneAmmo && machine is StonePile stonePile && stonePile.AmmoCount != state.StoneAmmo)
        {
            stonePile.SetAmmo(state.StoneAmmo);
        }

        if (state.HitPoints >= 0f && machine.DestructionComponent != null)
        {
            var destruction = machine.DestructionComponent;
            destruction.HitPoint = state.HitPoints;
            SyncMissionSiegeWeaponHealth(destruction, state.HitPoints);
            // Forward only: destruction states never regress, and vanilla's broken-entity swap indexes
            // _destructionStates[state - 1], so applying a lower state (a local cosmetic hit ran ahead)
            // would index out of range. forcedId -1 = don't force the broken entity's MissionObjectId,
            // matching vanilla's local destruction path.
            if (state.DestructionState > destruction._currentStateIndex)
            {
                destruction.SetDestructionLevel(state.DestructionState, -1, 0f, TaleWorlds.Library.Vec3.Zero, TaleWorlds.Library.Vec3.Zero);
            }
        }
    }

    private static void ApplyLadderAnimation(SiegeLadder ladder, NetworkSiegeLadderAnimationState state)
    {
        ladder._animationState = (SiegeLadder.LadderAnimationState)state.AnimationState;
        ladder._fallAngularSpeed = state.FallAngularSpeed;
        ladder._lastDotProductOfAnimationAndTargetRotation = -1000f;

        var ladderFrame = state.Frame;
        ladderFrame.rotation.Orthonormalize();
        ladder._ladderObject.GameEntity.SetGlobalFrame(ladderFrame);

        if (state.AnimationIndex < 0) return;

        ladder._ladderSkeleton.SetAnimationAtChannel(state.AnimationIndex, 0);
        ladder._ladderSkeleton.SetAnimationSpeedAtChannel(0, state.AnimationSpeed);
        ladder._ladderSkeleton.SetAnimationParameterAtChannel(
            0,
            MBMath.ClampFloat(state.AnimationProgress, 0f, 1f));
        ladder._ladderSkeleton.ForceUpdateBoneFrames();
    }

    internal static IEnumerable<SiegeLadder.LadderState> GetLadderTransitionStates(
        SiegeLadder.LadderState currentState,
        SiegeLadder.LadderState targetState)
    {
        if (targetState == SiegeLadder.LadderState.BeingRaised)
        {
            if (currentState == SiegeLadder.LadderState.OnLand
                || currentState == SiegeLadder.LadderState.FallToLand)
            {
                yield return SiegeLadder.LadderState.BeingRaisedStartFromGround;
            }
            else if (currentState == SiegeLadder.LadderState.OnWall
                || currentState == SiegeLadder.LadderState.FallToWall)
            {
                yield return SiegeLadder.LadderState.BeingPushedBackStartFromWall;
                yield return SiegeLadder.LadderState.BeingPushedBack;
                yield return SiegeLadder.LadderState.BeingPushedBackStopped;
            }
            else if (currentState == SiegeLadder.LadderState.BeingPushedBack
                || currentState == SiegeLadder.LadderState.BeingPushedBackStartFromWall)
            {
                yield return SiegeLadder.LadderState.BeingPushedBackStopped;
            }
        }
        else if (targetState == SiegeLadder.LadderState.BeingPushedBack)
        {
            if (currentState == SiegeLadder.LadderState.OnWall
                || currentState == SiegeLadder.LadderState.FallToWall)
            {
                yield return SiegeLadder.LadderState.BeingPushedBackStartFromWall;
            }
            else if (currentState == SiegeLadder.LadderState.OnLand
                || currentState == SiegeLadder.LadderState.FallToLand)
            {
                yield return SiegeLadder.LadderState.BeingRaisedStartFromGround;
                yield return SiegeLadder.LadderState.BeingRaised;
                yield return SiegeLadder.LadderState.BeingRaisedStopped;
            }
            else if (currentState == SiegeLadder.LadderState.BeingRaised
                || currentState == SiegeLadder.LadderState.BeingRaisedStartFromGround)
            {
                yield return SiegeLadder.LadderState.BeingRaisedStopped;
            }
        }
        else if (targetState == SiegeLadder.LadderState.OnWall
            && currentState != SiegeLadder.LadderState.FallToWall)
        {
            yield return SiegeLadder.LadderState.FallToWall;
        }
        else if (targetState == SiegeLadder.LadderState.OnLand
            && currentState != SiegeLadder.LadderState.FallToLand)
        {
            yield return SiegeLadder.LadderState.FallToLand;
        }
    }

    // MissionSiegeWeaponsController keeps campaign health in a separate MissionSiegeWeapon record. Directly
    // assigning DestructableComponent.HitPoint (the network apply above) does not raise vanilla's OnHitTaken,
    // so keep that backing record synchronized for a later host promotion/final engine-state report.
    private static void SyncMissionSiegeWeaponHealth(DestructableComponent destruction, float hitPoints)
    {
        var enginesLogic = Mission.Current?.GetMissionBehavior<MissionSiegeEnginesLogic>();
        if (enginesLogic == null) return;

        if (TrySyncMissionSiegeWeaponHealth(enginesLogic, BattleSideEnum.Attacker, destruction, hitPoints)) return;
        TrySyncMissionSiegeWeaponHealth(enginesLogic, BattleSideEnum.Defender, destruction, hitPoints);
    }

    private static bool TrySyncMissionSiegeWeaponHealth(MissionSiegeEnginesLogic enginesLogic, BattleSideEnum side,
        DestructableComponent destruction, float hitPoints)
    {
        var controller = enginesLogic.GetSiegeWeaponsController(side) as MissionSiegeWeaponsController;
        return controller != null
            && TrySyncBackingWeaponHealth(controller._deployedWeapons, destruction, hitPoints);
    }

    // Pure identity-map operation split from the mission lookup above so the host-migration invariant can be
    // regression-tested with a managed identity key, without initializing native ScriptComponentBehavior types.
    private static bool TrySyncBackingWeaponHealth<TKey>(
        IDictionary<TKey, MissionSiegeWeapon> deployedWeapons,
        TKey destruction,
        float hitPoints)
    {
        if (!deployedWeapons.TryGetValue(destruction, out var backingWeapon)) return false;

        backingWeapon.SetHealth(hitPoints);
        return true;
    }

    // Non-simulating client: drive the wind-up/reload arm animation from the replicated WeaponState, mirroring
    // vanilla's OnRangedSiegeWeaponStateChange (Reloading -> SetUpAnimations, ReloadingPaused -> pause). Runs only
    // on a transition, since the state message re-sends on any field change. Never assigns rangedWeapon.State —
    // its setter runs ShootProjectile and other simulator-only side effects. The release swing rides the fire message.
    private void ApplyWeaponAnimation(UsableMachine machine, int weaponState)
    {
        if (weaponState < 0 || !(machine is RangedSiegeWeapon rangedWeapon)) return;

        int id = machine.Id.Id;
        if (peerWeaponState.TryGetValue(id, out var previous) && previous == weaponState) return;
        peerWeaponState[id] = weaponState;

        var skeletons = rangedWeapon.SkeletonOwnerObjects;
        if (skeletons == null) return;

        switch ((RangedSiegeWeapon.WeaponState)weaponState)
        {
            case RangedSiegeWeapon.WeaponState.Reloading:
            {
                var animations = rangedWeapon.SetUpAnimations;
                for (int i = 0; i < skeletons.Length; i++)
                {
                    if (skeletons[i] == null) continue;
                    if (skeletons[i].GameEntity.IsSkeletonAnimationPaused())
                        skeletons[i].ResumeSkeletonAnimationSynched();
                    else if (animations != null && i < animations.Length && !string.IsNullOrEmpty(animations[i]))
                        skeletons[i].SetAnimationAtChannelSynched(animations[i], 0);
                }
                break;
            }
            case RangedSiegeWeapon.WeaponState.ReloadingPaused:
                foreach (var skeleton in skeletons)
                    skeleton?.PauseSkeletonAnimationSynched();
                break;
        }
    }

    public void CatchUpJoiner(string controllerId)
    {
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null || !Mission.Current.IsSiegeBattle) return;

            RefreshMachineCache();
            bool isHost = session.IsLocalHost;
            if (isHost)
            {
                foreach (var authorityEpoch in authorityEpochs.Values)
                {
                    if (authorityEpoch > session.HostEpoch) return;
                }

                ReannounceAuthorityAfterPromotion();
                // BR-102: the replay asserts arbitration authority NOW, so it carries the CURRENT epoch
                // (not the epoch each claim was granted under) — a joiner already holds the newest
                // assignment the server gave it on entry.
                foreach (var authority in authorityRevisions)
                {
                    network.Send(controllerId, new NetworkSiegeMachineAuthority(
                        authority.Key,
                        CurrentAuthorityOwner(authority.Key),
                        GetAuthorityEpoch(authority.Key),
                        authority.Value,
                        session.OwnControllerId));
                }
            }

            int sent = 0;
            var machineIds = new List<int>(machines.Count);
            foreach (var machine in machines)
            {
                machineIds.Add(machine.Id.Id);
            }

            sent = SendJoinStateSnapshots(
                isHost,
                session.OwnControllerId,
                machineIds,
                claimedMachines,
                (machineId, simulatedLocally) => CaptureState(machinesById[machineId], isHost, simulatedLocally),
                state => network.Send(controllerId, Stamp(state)));

            int ladderAnimationsSent = 0;
            foreach (var machine in machines)
            {
                if (!(machine is SiegeLadder ladder)
                    || !SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machine.Id.Id))
                {
                    continue;
                }

                network.Send(controllerId, Stamp(CaptureLadderAnimationState(ladder)));
                ladderAnimationsSent++;
            }

            if (sent > 0)
                Logger.Information("[BattleSync] Replayed {Count} siege machine state(s) to joining {Controller} as {Role}",
                    sent, controllerId, isHost ? "host" : "claimant");
            if (ladderAnimationsSent > 0)
                Logger.Information("[BattleSync] Replayed {Count} siege ladder animation state(s) to joining {Controller}",
                    ladderAnimationsSent, controllerId);
        });
    }

    // Every existing peer receives the join notification. The host supplies all host-owned fields; a non-host
    // supplies only machines it actually claims, directly from the live simulator rather than lastSent. That
    // makes a stable/arrived/idle machine available to the joiner even when no delta will ever fire again.
    private static int SendJoinStateSnapshots(
        bool isHost,
        string ownControllerId,
        IEnumerable<int> machineIds,
        IReadOnlyDictionary<int, string> claims,
        Func<int, bool, NetworkSiegeMachineState> capture,
        Action<NetworkSiegeMachineState> send)
    {
        int sent = 0;
        foreach (int machineId in machineIds)
        {
            bool simulatedLocally;
            if (isHost)
            {
                simulatedLocally = !claims.ContainsKey(machineId);
            }
            else
            {
                if (!claims.TryGetValue(machineId, out var owner) || owner != ownControllerId)
                    continue;
                simulatedLocally = true;
            }

            send(capture(machineId, simulatedLocally));
            sent++;
        }

        return sent;
    }
}
