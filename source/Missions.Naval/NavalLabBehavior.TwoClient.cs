#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipControl;
using NavalDLC.Missions.ShipInput;
using NavalDLC.View.MissionViews;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    internal int OwnSlot => Array.IndexOf(manifest.Controllers, ownControllerId);
    internal MissionShip LocalShip => OwnSlot >= 0 && OwnSlot < Ships.Length ? Ships[OwnSlot] : null;
    internal Agent LocalCaptain => OwnSlot >= 0 && (OwnSlot * NavalLabManifest.CrewPerShip) < Agents.Length
        ? Agents[OwnSlot * NavalLabManifest.CrewPerShip] : null;
    internal Action<NetworkNavalLabHelmInput> SendNativeInput;
    private long nativeInputSequence;
    private readonly NetworkNavalLabHelmInput[] lastReceivedNativeInput = new NetworkNavalLabHelmInput[2];
    private bool lastHelmPermission;
    private readonly Dictionary<int, NetworkNavalLabStations> appliedStations = new();
    // Committed oar machines per ship, resolved once; station keys are immutable for the fixture.
    private readonly ShipOarMachine[][] committedMachines = new ShipOarMachine[2][];
    internal Action<NetworkNavalLabStations> SendStationRelease;
    // Released crew stay released; the owner authors each release revision, the other client only replays it.
    private readonly bool[][] releasedStations = { new bool[4], new bool[4] };
    private readonly long[] stationReleaseRevisions = new long[2];
    private readonly double[][] unconfirmedReleaseDeadlines = { new double[4], new double[4] };
    // Only a rower once observed seated can be released; a failed initial seat stays a fault.
    private readonly bool[][] seatedStations = { new bool[4], new bool[4] };
    private object firstStationObservationFailure;
    private object firstStationStopEntry;
    private object FirstStationStopEntry => System.Threading.Volatile.Read(ref firstStationStopEntry);
    internal bool CanPrepareTwoClientDeployment => factoryMaterialized && factoryReleased
        && !terminal && factoryAuthorityValid?.Invoke() == true;

    internal bool OwnsFixedStationOrder(ShipOrder order) => order?._ownerShip?.ShipOrigin is NavalLabShipOrigin
        && order._ownerShip.ShipsLogic?.Mission == Mission;

    internal MissionShip GetLocalControlledShip()
    {
        var captain = LocalCaptain;
        var point = LocalShip?.ShipControllerMachine?.PilotStandingPoint;
        return nativeDeploymentComplete && !terminal && captain != null && point != null
            && captain == Mission.MainAgent && captain.IsPlayerControlled
            && captain.CurrentlyUsedGameObject == point && point.UserAgent == captain ? LocalShip : null;
    }

    private bool HasNativeInputPermission(MissionShipControlView view) => NativeInputBlocker(view) == null;

    private string NativeInputBlocker(MissionShipControlView view)
    {
        if (Mission == null || Mission != Mission.Current || !CanUseNativeInput) return "native_input_not_ready";
        if (view == null || GetLocalControlledShip() == null || view.ControllerMachine != LocalShip.ShipControllerMachine)
            return "owner_helm_or_view_unavailable";
        if (view.MissionScreen == null || ScreenManager.TopScreen != view.MissionScreen) return "mission_screen_not_top";
        if (!ScreenManager._isWindowFocused) return "window_not_focused";
        if (view.MissionScreen.IsCheatGhostMode || view.MissionScreen.IsPhotoModeEnabled || view.IsDisplayingADialog) return "modal_photo_or_ghost";
        return null;
    }

    internal void RouteNativeAxes(MissionShipControlView view)
    {
        bool permission = HasNativeInputPermission(view);
        if (pulsePending && (Mission != Mission.Current || !permission || view != pulseView))
        {
            CancelAxesPulse("permission_lost_safety_stop");
            return;
        }
        if (pulsePending && ControlNow >= pulseDeadline)
        {
            pulsePending = false;
            pulseCompleting = true;
            pulsePhysicsObservation.Close();
        }
        var input = ShipInputRecord.Stop();
        if (permission)
        {
            var axes = pulsePending ? pulseAxes : new Vec2(view.Input.GetGameKeyAxis("MovementAxisX"), view.Input.GetGameKeyAxis("MovementAxisY"));
            if (pulseCompleting) axes = Vec2.Zero;
            if (Math.Abs(axes.x) <= 0.2f) axes.x = 0;
            if (Math.Abs(axes.y) <= 0.2f) axes.y = 0;
            view.TickRowerInput(axes, out var longitudinal, out var doubleTap, out var lateral);
            if (pulsePending && pulseRowStop) { longitudinal = RowerLongitudinalInput.Stop; doubleTap = RowerLongitudinalInput.None; }
            input = new ShipInputRecord(lateral, longitudinal, doubleTap, view.TickRudderInput(axes), view.SailControl);
        }
        nativeInputCallback++;
        lastHelmPermission = permission;
        var message = new NetworkNavalLabHelmInput(manifest.IncarnationId, 1, OwnSlot, ++nativeInputSequence,
            pulsePending ? pulseDeadlineUtcTicks : DateTime.UtcNow.AddSeconds(1).Ticks, permission,
            (int)input.RowerLateral, (int)input.RowerLongitudinal,
            (int)input.RowerLongitudinalDoubleTap, input.RudderLateral, (int)input.Sail);
        try { SendNativeInput?.Invoke(message); }
        catch
        {
            if (pulsePending || pulseCompleting)
            {
                pulsePending = pulseCompleting = false;
                pulsePhysicsObservation.Close();
                pulsePhase = "failed_dispatch_safety_hold";
            }
            throw;
        }
        lastSentNativeInput = message;
        if (pulsePending)
        {
            if (pulseFirstInputSequence == 0) pulseFirstInputSequence = message.Sequence;
            pulseLastInputSequence = message.Sequence;
        }
        if (pulseCompleting)
        {
            pulseNeutralInputSequence = message.Sequence;
            pulsePhase = "completed_axes_neutral_requested";
        }
        pulseCompleting = false;
    }

    internal void ApplyNativeInput(NetworkNavalLabHelmInput input)
    {
        if (!CanUseNativeInput || input.Ship != OwnSlot || !input.IsValid) return;
        var record = new ShipInputRecord((RowerLateralInput)input.Lateral, (RowerLongitudinalInput)input.Longitudinal,
            (RowerLongitudinalInput)input.DoubleTap, input.Rudder, (SailInput)input.Sail);
        Ships[input.Ship].PlayerController.SetInput(in record);
        nativeInputApplyCallback = nativeInputCallback;
        lastReceivedNativeInput[input.Ship] = input;
    }

    internal void NeutralizeNativeInput(int slot)
    {
        if (slot != OwnSlot || slot < 0 || slot >= Ships.Length || Ships[slot]?.Controller is not PlayerShipController player) return;
        var stop = ShipInputRecord.Stop();
        player.SetInput(in stop);
        nativeInputApplyCallback = nativeInputCallback;
    }

    private string StationKey(UsableMachine machine, MissionShip ship) => EntityKey(machine.PilotStandingPoint.GameEntity, ship);

    private string EntityKey(WeakGameEntity entity, MissionShip ship)
    {
        // Named child paths are content identities, never process-local native pointers.
        var parts = new List<string>();
        while (entity.IsValid && entity != ship.GameEntity && parts.Count < 16)
        {
            var parent = entity.Parent;
            if (!parent.IsValid) throw new InvalidOperationException("native.station_outside_hull");
            int index = parent.GetChildren().ToList().IndexOf(entity);
            if (index < 0) throw new InvalidOperationException("native.station_child_missing");
            parts.Add(index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + entity.Name);
            entity = parent;
        }
        if (!entity.IsValid || entity != ship.GameEntity) throw new InvalidOperationException("native.station_outside_hull");
        parts.Reverse();
        return string.Join("/", parts);
    }

    private Dictionary<string, ShipOarMachine> StationInventory(int slot)
    {
        if (!nativeDeploymentComplete || terminal || Blocker != null
            || slot < 0 || slot >= Ships.Length) throw new InvalidOperationException("native.station_lifecycle");
        var ship = Ships[slot];
        if (!ship.IsDeployed || ship.ShipOrigin is not NavalLabShipOrigin || ship.ShipOrigin.Hull != hull)
            throw new InvalidOperationException("native.station_hull");
        var result = new Dictionary<string, ShipOarMachine>(StringComparer.Ordinal);
        foreach (var machine in ship.ShipOarMachines)
        {
            var point = machine.PilotStandingPoint;
            if (machine.GetType() != typeof(ShipOarMachine) || point == null || point.GetType() != typeof(StandingPoint) || !point.GameEntity.IsValid
                || point.GetComponent<ResetAnimationOnStopUsageComponent>() == null || machine._oar == null)
                throw new InvalidOperationException("native.station_prerequisite");
            var key = StationKey(machine, ship);
            if (string.IsNullOrWhiteSpace(key) || key.Length > 256 || result.ContainsKey(key))
                throw new InvalidOperationException("native.ambiguous_station_identity");
            result.Add(key, machine);
        }
        if (result.Count < 4) throw new InvalidOperationException("native.insufficient_oar_stations");
        return result;
    }

    internal NetworkNavalLabStations CreateStations()
    {
        var inventory = StationInventory(OwnSlot).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        if (LocalShip.LeftSideShipOarMachines.Count < 2 || LocalShip.RightSideShipOarMachines.Count < 2)
            throw new InvalidOperationException("native.insufficient_paired_oars");
        // Fixed initial crew only; automatic weapon/helm detachment allocation is disabled in this mode.
        return new NetworkNavalLabStations(manifest.IncarnationId, 1, OwnSlot, "offer",
            manifest.Combatants.Skip((OwnSlot * 5) + 1).Take(4).ToArray(),
            LocalShip.LeftSideShipOarMachines.Take(2).Concat(LocalShip.RightSideShipOarMachines.Take(2))
                .Select(machine => StationKey(machine, LocalShip)).ToArray(),
            LocalShip.Sails.Select(sail => SailKey(sail, LocalShip)).ToArray(),
            inventory.Select(pair => pair.Key).ToArray(), inventory.Select(pair => (int)pair.Value._oar._sidePhaseData.Side).ToArray());
    }

    internal void ApplyStations(NetworkNavalLabStations value)
    {
        if (value.Phase == "release") { ApplyStationRelease(value); return; }
        var inventory = StationInventory(value.Ship);
        var ordered = inventory.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        if (!value.HasPresentationInventory || !value.SailKeys.SequenceEqual(Ships[value.Ship].Sails.Select(sail => SailKey(sail, Ships[value.Ship])))
            || Ships[value.Ship].Sails.Any(sail => (int)sail.SailObject.Type != 0)
            || !value.OarKeys.SequenceEqual(ordered.Select(pair => pair.Key))
            || !value.OarSides.SequenceEqual(ordered.Select(pair => (int)pair.Value._oar._sidePhaseData.Side)))
            throw new InvalidOperationException("native.presentation_inventory_mismatch");
        if (value.IncarnationId != manifest.IncarnationId || value.Epoch != 1 || value.Phase != "commit"
            || value.Combatants == null || value.Keys == null || value.Keys.Length != 4 || value.Combatants.Length != 4
            || value.Keys.Distinct().Count() != 4
            || !value.Combatants.SequenceEqual(manifest.Combatants.Skip((value.Ship * 5) + 1).Take(4)))
            throw new InvalidOperationException("native.invalid_station_commit");
        if (appliedStations.TryGetValue(value.Ship, out var prior))
        {
            if (!prior.Keys.SequenceEqual(value.Keys) || !ObserveStations(prior)) throw new InvalidOperationException("native.conflicting_station_commit");
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            var agent = Agents[(value.Ship * 5) + i + 1];
            if (!inventory.TryGetValue(value.Keys[i], out var machine) || agent == null || !agent.IsActive()
                || agent.Formation != Ships[value.Ship].Formation || agent.CurrentlyUsedGameObject != null
                || (manifest.Controllers[value.Ship] == ownControllerId ? !agent.IsAIControlled : agent.Controller != AgentControllerType.None)
                || machine.PilotStandingPoint.UserAgent != null || machine.PilotStandingPoint.MovingAgent != null
                || machine.PilotStandingPoint.IsDeactivated || machine._oar.OwnerShip != Ships[value.Ship])
                throw new InvalidOperationException("native.occupied_or_invalid_station");
        }
        appliedStations.Add(value.Ship, value);
        committedMachines[value.Ship] = value.Keys.Select(key => inventory[key]).ToArray();
        for (int i = 0; i < 4; i++)
        {
            var machine = inventory[value.Keys[i]];
            var agent = Agents[(value.Ship * 5) + i + 1];
            agent.UseGameObject(machine.PilotStandingPoint);
            machine.OnPilotAssignedDuringSpawn();
        }
        if (!ObserveStations(value)) throw new InvalidOperationException("native.station_apply_not_observed");
    }

    internal bool IsCommittedOarMovement(Guid incarnationId, Guid combatantId, Agent agent)
    {
        int combatant = Array.IndexOf(manifest.Combatants, combatantId);
        int slot = combatant < 0 ? -1 : combatant / NavalLabManifest.CrewPerShip;
        if (incarnationId != manifest.IncarnationId || Mission == null || Mission != Mission.Current
            || terminal || nativeTerminalHold || !nativeDeploymentComplete || Blocker != null
            || slot < 0 || slot >= Ships.Length || !appliedStations.TryGetValue(slot, out var stations)
            || stations.IncarnationId != incarnationId || stations.Epoch != 1 || stations.Phase != "commit") return false;
        int crew = Array.IndexOf(stations.Combatants, combatantId);
        int index = (slot * NavalLabManifest.CrewPerShip) + crew + 1;
        // The original owner's rower stays AI-driven; its replica on the other client is a controller-less puppet.
        if (crew < 0 || crew >= 4 || releasedStations[slot][crew] || stations.Keys == null || stations.Keys.Length != 4
            || index >= Agents.Length || manifest.Combatants[index] != combatantId || Agents[index] != agent
            || agent == null || agent.Pointer == UIntPtr.Zero || agent.Mission != Mission || !agent.IsActive()
            || agent == Mission.MainAgent || agent.IsMainAgent || !agent.IsHuman
            || (slot == OwnSlot ? !agent.IsAIControlled : agent.Controller != AgentControllerType.None)
            || agent.MountAgent != null || agent.IsMount) return false;
        ShipOarMachine machine;
        try { machine = CommittedMachines(stations)[crew]; }
        catch (InvalidOperationException) { return false; }
        if (machine == null) return false;
        var point = machine.PilotStandingPoint;
        var ship = Ships[slot];
        return ship.GameEntity.IsValid && machine.GameEntity.IsValid && point != null && point.GameEntity.IsValid
            && !point.IsDeactivated && ship.Captain != agent && agent.Formation == ship.Formation
            && machine._oar?.OwnerShip == ship && machine.PilotAgent == agent && point.UserAgent == agent
            && agent.CurrentlyUsedGameObject == point && machine._isPilotSitting && machine._lastPilotAgent == agent
            && point.LockUserFrames && agent.MovementLockedState == AgentMovementLockedState.FrameLocked;
    }

    internal void RefreshFollowerStationTargets(int slot = -1)
    {
        if (!factoryReleased || terminal || nativeTerminalHold
            || Mission == null || Mission != Mission.Current)
            throw new InvalidOperationException("native.station_target_lifecycle");
        foreach (var stations in appliedStations.Values.Where(stations => stations.Ship == slot && !OwnsFactoryHull(stations.Ship)))
        {
            if (stations.IncarnationId != manifest.IncarnationId || stations.Epoch != 1 || stations.Phase != "commit"
                || !ObserveStations(stations, refreshTargets: true))
                throw new InvalidOperationException("native.station_occupancy_lost");
        }
        if (slot != OwnSlot) RefreshReplicatedFollowerHelmTarget();
        if (slot >= 0) shipTargetRefreshes[slot]++;
    }

    private ShipOarMachine[] CommittedMachines(NetworkNavalLabStations stations)
    {
        if (committedMachines[stations.Ship] != null) return committedMachines[stations.Ship];
        var inventory = StationInventory(stations.Ship);
        var machines = stations.Keys.Select(key => inventory.TryGetValue(key, out var machine) ? machine : null).ToArray();
        if (machines.All(machine => machine != null)) committedMachines[stations.Ship] = machines;
        return machines;
    }

    internal bool ObserveStations(NetworkNavalLabStations value) => ObserveStations(value, refreshTargets: false);

    private bool ObserveStations(NetworkNavalLabStations value, bool refreshTargets)
    {
        var machines = CommittedMachines(value);
        for (int i = 0; i < 4; i++)
        {
            var machine = machines[i];
            if (machine == null)
                return RecordStationObservationFailure(value, i, "station_missing", refreshTargets, null, null);
            var agent = Agents[(value.Ship * 5) + i + 1];
            var point = machine.PilotStandingPoint;
            var ship = Ships[value.Ship];
            // A released rower is neither seat-checked nor repinned to its station target.
            if (releasedStations[value.Ship][i]) continue;
            if (seatedStations[value.Ship][i] && IsNativeStationRelease(value.Ship, agent, machine))
            {
                if (!ObserveNativeStationRelease(value, i, refreshTargets, machine, agent)) return false;
                continue;
            }
            if (refreshTargets && (agent == null || agent.Pointer == UIntPtr.Zero || agent.Mission != Mission
                || agent == Mission.MainAgent || agent.IsMainAgent || !agent.IsHuman || agent.MountAgent != null || agent.IsMount
                || !ship.GameEntity.IsValid || !machine.GameEntity.IsValid || point == null || !point.GameEntity.IsValid
                || point.IsDeactivated || ship.Captain == agent || agent.Formation != ship.Formation
                || machine._oar?.OwnerShip != ship || !point.LockUserFrames
                || agent.MovementLockedState != AgentMovementLockedState.FrameLocked))
                return RecordStationObservationFailure(value, i, "target_refresh_precondition", refreshTargets, machine, agent);
            string failure = agent == null ? "agent_missing"
                : !agent.IsActive() ? "agent_inactive"
                : (manifest.Controllers[value.Ship] == ownControllerId ? !agent.IsAIControlled : agent.Controller != AgentControllerType.None) ? "controller_mismatch"
                : machine.PilotAgent != agent ? "pilot_mismatch"
                : machine.PilotStandingPoint.UserAgent != agent ? "point_user_mismatch"
                : agent.CurrentlyUsedGameObject != machine.PilotStandingPoint ? "used_object_mismatch"
                : !machine._isPilotSitting ? "not_sitting"
                : machine._lastPilotAgent != agent ? "last_pilot_mismatch" : null;
            if (failure != null) return RecordStationObservationFailure(value, i, failure, refreshTargets, machine, agent);
            seatedStations[value.Ship][i] = true;
            if (refreshTargets)
            {
                var frame = point.GetUserFrameForAgent(agent);
                agent.SetTargetPositionAndDirection(frame.Origin.AsVec2, in frame.Rotation.f);
            }
        }
        return true;
    }

    // A complete native stop-use by a still-valid crew actor, not a partial or foreign occupancy.
    private bool IsNativeStationRelease(int slot, Agent agent, ShipOarMachine machine) =>
        agent != null && agent.IsActive() && machine.PilotAgent == null && machine.PilotStandingPoint.UserAgent == null
        && agent.CurrentlyUsedGameObject != machine.PilotStandingPoint
        && (manifest.Controllers[slot] == ownControllerId ? agent.IsAIControlled : agent.Controller == AgentControllerType.None);

    private bool ObserveNativeStationRelease(NetworkNavalLabStations value, int crew, bool refreshTargets, ShipOarMachine machine, Agent agent)
    {
        int slot = value.Ship;
        if (manifest.Controllers[slot] != ownControllerId)
        {
            // Bridge side effects can unseat a replica before the owner's release arrives; only an unconfirmed release faults.
            if (unconfirmedReleaseDeadlines[slot][crew] == 0) unconfirmedReleaseDeadlines[slot][crew] = ControlNow + 2;
            return ControlNow < unconfirmedReleaseDeadlines[slot][crew]
                || RecordStationObservationFailure(value, crew, "release_unconfirmed_by_owner", refreshTargets, machine, agent);
        }
        if (SendStationRelease == null)
            return RecordStationObservationFailure(value, crew, "release_without_sender", refreshTargets, machine, agent);
        releasedStations[slot][crew] = true;
        SendStationRelease(appliedStations[slot].WithRelease(++stationReleaseRevisions[slot], releasedStations[slot]));
        return true;
    }

    // Replays the owner's ordered release through native stop-use; a stale revision never repins a released rower.
    private void ApplyStationRelease(NetworkNavalLabStations value)
    {
        int slot = value.Ship;
        if (value.IncarnationId != manifest.IncarnationId || value.Epoch != 1 || slot < 0 || slot >= Ships.Length
            || manifest.Controllers[slot] == ownControllerId || !appliedStations.TryGetValue(slot, out var committed)
            || !committed.Keys.SequenceEqual(value.Keys) || !committed.Combatants.SequenceEqual(value.Combatants)
            || value.Released == null || value.Released.Length != 4)
            throw new InvalidOperationException("native.station_release_identity");
        if (value.Revision <= stationReleaseRevisions[slot]) return;
        if (value.Revision != stationReleaseRevisions[slot] + 1
            || Enumerable.Range(0, 4).Any(crew => releasedStations[slot][crew] && !value.Released[crew]))
            throw new InvalidOperationException("native.station_release_order");
        var inventory = StationInventory(slot);
        for (int crew = 0; crew < 4; crew++)
        {
            if (!value.Released[crew] || releasedStations[slot][crew]) continue;
            var agent = Agents[(slot * 5) + crew + 1];
            var point = inventory[value.Keys[crew]].PilotStandingPoint;
            if (agent == null || !agent.IsActive() || agent.Controller != AgentControllerType.None
                || (point.UserAgent != null && point.UserAgent != agent))
                throw new InvalidOperationException("native.station_release_actor");
            if (point.UserAgent == agent || agent.CurrentlyUsedGameObject == point) agent.StopUsingGameObject();
            releasedStations[slot][crew] = true;
            unconfirmedReleaseDeadlines[slot][crew] = 0;
        }
        stationReleaseRevisions[slot] = value.Revision;
    }

    internal bool CanTraceStationStop(ShipOarMachine machine)
    {
        if (!HasRopeExperiment || !nativeDeploymentComplete || !factoryReleased || terminal || nativeTerminalHold
            || Blocker != null || FirstStationStopEntry != null) return false;
        var inventory = presentationInventory;
        if (inventory == null) return false;
        foreach (var entry in inventory)
        {
            int index = Array.IndexOf(entry.Machines, machine);
            if (index < 0) continue;
            var agent = entry.Actors[index];
            var point = machine.PilotStandingPoint;
            return agent != null && point != null && ReferenceEquals(point.UserAgent, agent)
                && ReferenceEquals(agent.CurrentlyUsedGameObject, point);
        }
        return false;
    }

    internal void RecordStationStopEntry(Agent agent, bool successful, Agent.StopUsingGameObjectFlags flags)
    {
        if (!HasRopeExperiment || !nativeDeploymentComplete || !factoryReleased || terminal || nativeTerminalHold
            || Blocker != null || agent == null || FirstStationStopEntry != null) return;
        var inventory = presentationInventory;
        if (inventory == null) return;
        for (int slot = 0; slot < inventory.Length; slot++)
        {
            var entry = inventory[slot];
            for (int crew = 0; crew < entry.Actors.Length; crew++)
            {
                if (!ReferenceEquals(entry.Actors[crew], agent)) continue;
                var machine = entry.Machines[crew];
                var point = machine.PilotStandingPoint;
                // Match published fixture references only; this hook may run on a parallel native callback.
                if (point == null || !ReferenceEquals(agent.CurrentlyUsedGameObject, point)
                    || !ReferenceEquals(point.UserAgent, agent)) return;
                var stack = new List<object>();
                for (int i = 0; i < 8; i++)
                {
                    var frame = new System.Diagnostics.StackFrame(i + 1, false);
                    var method = frame.GetMethod();
                    if (method == null) break;
                    string name = method.DeclaringType?.FullName + "." + method.Name;
                    stack.Add(new { method = name.Length > 180 ? name.Substring(0, 180) : name, ilOffset = frame.GetILOffset() });
                }
                object record = new
                {
                    slot, stationIndex = crew, key = entry.OarKeys[crew], combatant = entry.Combatants[crew], owner = manifest.Controllers[slot],
                    successful, flags = flags.ToString(), threadId = System.Threading.Thread.CurrentThread.ManagedThreadId,
                    timestamp = System.Diagnostics.Stopwatch.GetTimestamp(),
                    localMissionTick = System.Threading.Interlocked.Read(ref nativeHelmTicks),
                    pilotMatches = ReferenceEquals(machine.PilotAgent, agent), machine._isPilotSitting,
                    lastPilotMatches = ReferenceEquals(machine._lastPilotAgent, agent), stack = stack.ToArray(),
                    oarCallsite = NavalLabPresentationPatches.StationStopCallsite(agent),
                    observation = "First exact seated rower StopUsingGameObjectAux entry, before original cleanup; not proof of completion. No native pointer reads."
                };
                System.Threading.Interlocked.CompareExchange(ref firstStationStopEntry, record, null);
                return;
            }
        }
    }

    private bool RecordStationObservationFailure(NetworkNavalLabStations value, int crew, string reason,
        bool refreshTargets, ShipOarMachine machine, Agent agent)
    {
        if (firstStationObservationFailure != null) return false;
        object native = null;
        string unavailable = null;
        try
        {
            var point = machine?.PilotStandingPoint;
            if (Mission == null || Mission != Mission.Current || agent == null || agent.Pointer == UIntPtr.Zero
                || agent.Mission != Mission || machine == null || !machine.GameEntity.IsValid
                || point == null || !point.GameEntity.IsValid) unavailable = "native_identity_unavailable";
            else native = new
            {
                agentIndex = agent.Index, controller = agent.Controller.ToString(), active = agent.IsActive(),
                pointId = point.Id.Id, machineId = machine.Id.Id,
                pilotIndex = machine.PilotAgent?.Index, pointUserIndex = point.UserAgent?.Index,
                usedObjectId = agent.CurrentlyUsedGameObject?.Id.Id, lastPilotIndex = machine._lastPilotAgent?.Index,
                sitting = machine._isPilotSitting, point.IsDeactivated, point.LockUserFrames,
                movementLockedState = agent.MovementLockedState.ToString(),
                disablingRampCount = machine._disablingAttachmentRampEntities.Count,
                pilotRemovalTime = RopeNumber(machine._pilotRemovalTime.Item1),
                pilotRemovalFlags = machine._pilotRemovalTime.Item2.ToString(),
                action0 = agent.GetCurrentAction(0).Index, action1 = agent.GetCurrentAction(1).Index
            };
        }
        catch (Exception exception) { unavailable = exception.GetType().Name; }
        // Retain the first failed predicate before terminal cleanup can clear native identities.
        firstStationObservationFailure = new
        {
            reason, ship = value.Ship, crew, key = value.Keys[crew], combatant = value.Combatants[crew],
            owner = manifest.Controllers[value.Ship], localMissionTick = nativeHelmTicks,
            refreshTargets, native, unavailable,
            observation = "First failed station predicate; extra scalar reads are later, non-atomic, and do not identify the writer."
        };
        return false;
    }
}
#endif
