using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Replicates the gameplay state of siege machines from the mission host to every other client.
/// The host simulates all machines (its agents man them; see SiegeMachineAuthorityPatches); this
/// service polls their state a few times a second, broadcasts changes, and applies received state on
/// non-authority clients — whose machines it also deactivates so their own troops never man them.
/// Nothing irreversible happens until the host election result is known, and a client promoted to
/// host reactivates the machines it had deactivated. Ranged machine aim and projectiles stay
/// host-local visuals; agent damage from them routes through the existing owner-authoritative blow
/// path, and wall/gate/machine damage arrives as the hit point and destruction state synced here.
/// </summary>
public interface ISiegeMachineStateReplicator : IDisposable
{
    /// <summary>[Game thread] Poll for changes (host) or machines to deactivate (peers).</summary>
    void Tick(float dt);

    /// <summary>[Host] Replay every machine's current state to a joining controller.</summary>
    void CatchUpJoiner(string controllerId);
}

/// <inheritdoc cref="ISiegeMachineStateReplicator"/>
public class SiegeMachineStateReplicator : ISiegeMachineStateReplicator
{
    private const float PollInterval = 0.25f;

    private static readonly ILogger Logger = LogManager.GetLogger<SiegeMachineStateReplicator>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;

    // Machine cache + last broadcast state per machine id (host) / deactivated ids (peers).
    // Game-thread only; reset when the mission changes (MissionObjectIds recycle across missions).
    private readonly List<UsableMachine> machines = new List<UsableMachine>();
    private readonly Dictionary<int, UsableMachine> machinesById = new Dictionary<int, UsableMachine>();
    private readonly Dictionary<int, NetworkSiegeMachineState> lastSent = new Dictionary<int, NetworkSiegeMachineState>();
    private readonly HashSet<int> deactivated = new HashSet<int>();
    // States that arrived before their MissionObject registered (catch-up during a peer's scene load);
    // re-applied once the object appears. Keyed by machine id, latest state wins.
    private readonly Dictionary<int, NetworkSiegeMachineState> pendingByMachineId = new Dictionary<int, NetworkSiegeMachineState>();
    private Mission trackedMission;
    private int trackedObjectCount;
    private float pollTimer;

    public SiegeMachineStateReplicator(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;

        messageBroker.Subscribe<NetworkSiegeMachineState>(Handle_NetworkMachineState);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSiegeMachineState>(Handle_NetworkMachineState);
    }

    public void Tick(float dt)
    {
        if (Mission.Current == null || !Mission.Current.IsSiegeBattle) return;

        pollTimer += dt;
        if (pollTimer < PollInterval) return;
        pollTimer = 0f;

        RefreshMachineCache();
        DrainPendingMachineStates();

        // Until the election result is stored, "not host" means "unknown" — take no irreversible step.
        if (!SiegeMissionAuthorityGate.IsAuthorityKnown) return;

        if (session.IsLocalHost)
        {
            ReactivateIfPromoted();
            BroadcastChangedStates();
        }
        else
        {
            DeactivateNewMachines();
        }
    }

    private void RefreshMachineCache()
    {
        var mission = Mission.Current;
        if (trackedMission == mission && trackedObjectCount == mission.MissionObjects.Count) return;

        if (trackedMission != mission)
        {
            lastSent.Clear();
            deactivated.Clear();
            pendingByMachineId.Clear();
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

    // A successor promoted to host had deactivated its machines as a peer; the sole simulating
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
        foreach (var machine in machines)
        {
            ReadState(machine, out var hitPoints, out var destructionState, out var gateState, out var ladderState, out var moveDistance, out var hasArrived);

            if (lastSent.TryGetValue(machine.Id.Id, out var previous)
                && previous.HitPoints == hitPoints
                && previous.DestructionState == destructionState
                && previous.GateState == gateState
                && previous.LadderState == ladderState
                && previous.HasArrived == hasArrived
                && Math.Abs(previous.MoveDistance - moveDistance) < 0.5f)
            {
                continue;
            }

            var state = new NetworkSiegeMachineState(machine.Id.Id, hitPoints, destructionState, gateState, ladderState, moveDistance, hasArrived);
            lastSent[machine.Id.Id] = state;
            network.SendAll(state);
        }
    }

    private void DeactivateNewMachines()
    {
        if (deactivated.Count == machines.Count) return;

        foreach (var machine in machines)
        {
            // Keep the primary siege weapons (rams/towers/ladders) live on a peer: it still runs its own attacker
            // AI, and BehaviorAssaultWalls strips deactivated primary weapons then MaxBy-crashes on the empty set.
            // Their authoritative state still rides the host's per-tick BroadcastChangedStates.
            if (machine is IPrimarySiegeWeapon) continue;

            if (!deactivated.Add(machine.Id.Id)) continue;

            machine.Deactivate();
        }
    }

    // Re-apply buffered states whose MissionObject has now registered. Runs on every client each poll; the
    // host's buffer stays empty (it never receives these), so this is a no-op there.
    private void DrainPendingMachineStates()
    {
        if (pendingByMachineId.Count == 0) return;

        List<int> applied = null;
        foreach (var pending in pendingByMachineId)
        {
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

            if (applied == null) applied = new List<int>();
            applied.Add(pending.Key);
        }

        if (applied == null) return;
        foreach (var id in applied) pendingByMachineId.Remove(id);
    }

    private static void ReadState(UsableMachine machine, out float hitPoints, out int destructionState,
        out int gateState, out int ladderState, out float moveDistance, out bool hasArrived)
    {
        hitPoints = -1f;
        destructionState = -1;
        if (machine.DestructionComponent != null)
        {
            hitPoints = machine.DestructionComponent.HitPoint;
            destructionState = machine.DestructionComponent._currentStateIndex;
        }

        gateState = machine is CastleGate gate ? (int)gate.State : -1;
        ladderState = machine is SiegeLadder ladder ? (int)ladder.State : -1;

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

            RefreshMachineCache();
            if (!machinesById.TryGetValue(obj.MachineId, out var machine))
            {
                // A net-zero MissionObjects churn (an item spawned and despawned within a poll) can hide
                // a newly-added machine from the count-gated cache; force a rebuild before giving up.
                trackedMission = null;
                RefreshMachineCache();
                if (!machinesById.TryGetValue(obj.MachineId, out machine))
                {
                    // The MissionObject isn't registered yet (catch-up during scene load); buffer and re-apply
                    // from Tick once RefreshMachineCache sees it, instead of dropping it permanently.
                    pendingByMachineId[obj.MachineId] = obj;
                    return;
                }
            }

            SiegeMissionAuthorityGate.SuppressCapture = true;
            try
            {
                Apply(machine, obj);
            }
            finally
            {
                SiegeMissionAuthorityGate.SuppressCapture = false;
            }
        });
    }

    private static void Apply(UsableMachine machine, NetworkSiegeMachineState state)
    {
        if (state.MoveDistance >= 0f)
        {
            // The vanilla client-side apply recipe (BatteringRam.OnAfterReadFromNetwork): distance
            // first, then the arrival flag whose setter flips the navmesh.
            if (machine is BatteringRam ram)
            {
                ram.MovementComponent.SetTotalDistanceTraveledForPathTracker(state.MoveDistance);
                ram.MovementComponent.SetTargetFrameForPathTracker();
                if (state.HasArrived && !ram.HasArrivedAtTarget) ram.HasArrivedAtTarget = true;
            }
            else if (machine is SiegeTower tower)
            {
                tower.MovementComponent.SetTotalDistanceTraveledForPathTracker(state.MoveDistance);
                tower.MovementComponent.SetTargetFrameForPathTracker();
                if (state.HasArrived && !tower.HasArrivedAtTarget) tower.HasArrivedAtTarget = true;
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

        if (state.LadderState >= 0 && machine is SiegeLadder ladder && (int)ladder.State != state.LadderState)
        {
            ladder.State = (SiegeLadder.LadderState)state.LadderState;
        }

        if (state.HitPoints >= 0f && machine.DestructionComponent != null)
        {
            var destruction = machine.DestructionComponent;
            destruction.HitPoint = state.HitPoints;
            if (state.DestructionState >= 0 && destruction._currentStateIndex != state.DestructionState)
            {
                destruction.SetDestructionLevel(state.DestructionState, 0, 0f, TaleWorlds.Library.Vec3.Zero, TaleWorlds.Library.Vec3.Zero);
            }
        }
    }

    public void CatchUpJoiner(string controllerId)
    {
        if (!session.IsLocalHost) return;

        GameThread.RunSafe(() =>
        {
            foreach (var state in lastSent.Values)
            {
                network.Send(controllerId, state);
            }

            if (lastSent.Count > 0)
                Logger.Information("[BattleSync] Replayed {Count} siege machine state(s) to joining {Controller}", lastSent.Count, controllerId);
        });
    }
}
