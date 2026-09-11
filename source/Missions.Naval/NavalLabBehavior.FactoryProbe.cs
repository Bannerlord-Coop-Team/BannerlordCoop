#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using TaleWorlds.Engine;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    private Func<bool> factoryAuthorityValid;
    private bool factoryHost;
    private bool factoryMaterialized;
    private bool factoryAttempted;
    private bool factoryReleased;
    private bool factoryTerminal;
    private volatile bool factoryObserving;
    private volatile MissionShip[] completedFactoryHulls = Array.Empty<MissionShip>();
    private int factorySlot = -1;
    private long factoryPreCompletionFixedEntries;
    private long factoryParallelEntries;
    private long factoryActiveParallelEntries;
    private readonly List<object> factoryTrace = new List<object>();

    internal void MaterializeFactoryProbe(bool electedHost, Func<bool> authorityValid)
    {
        if (!IsFactoryProbe || factoryAttempted || factoryTerminal)
            throw new InvalidOperationException("factory_probe.invalid_lifecycle");
        if (authorityValid == null || !authorityValid()) throw new InvalidOperationException("factory_probe.no_assignment");
        factoryAttempted = true;
        factoryHost = electedHost;
        factoryAuthorityValid = authorityValid;
        factoryObserving = true;
        try
        {
            InitializeFixture();
            CheckFactoryAssignment();
            if (completedFactoryHulls.Length != manifest.Ships.Length)
                throw new InvalidOperationException("factory_probe.missing_completed_hook");
            factoryMaterialized = true;
            Simulating = factoryHost;
            RecordFactoryPhase("materialized", null);
            RecordStartup("fixture_initialized");
        }
        catch (Exception exception)
        {
            Reject(exception.ToString());
            // Only successful, fully checked initialization returns are eligible for terminal body hold.
            HoldFactoryProbe();
            throw;
        }
    }

    private void CheckFactoryAssignment()
    {
        if (factoryTerminal || Blocker != null || factoryAuthorityValid?.Invoke() != true)
            throw new InvalidOperationException(Blocker ?? "factory_probe.assignment_changed");
    }

    private void BeginFactoryHull(int slot)
    {
        CheckFactoryAssignment();
        factorySlot = slot;
        RecordFactoryPhase("factory_begin", null);
    }

    internal void CompleteFactoryHull(MissionShip ship)
    {
        if (!IsFactoryProbe || !factoryAttempted) return;
        if (factorySlot < 0 || factorySlot >= Ships.Length || completedFactoryHulls.Contains(ship)
            || ship == null || !ship.GameEntity.IsValid || !ship.IsInitialized || ship._actuators == null
            || ship.Physics?.IsInitialized != true || ship.Formation == null || ship.ShipOrder == null
            || ship.ShipOrigin is not NavalLabShipOrigin
            || ship.ShipsLogic?.Mission != Mission || ship.Physics.GameEntity.Pointer != ship.GameEntity.Pointer
            || !ship.GameEntity.HasDynamicRigidBody())
            throw new InvalidOperationException("factory_probe.incomplete_native_return");
        completedFactoryHulls = completedFactoryHulls.Concat(new[] { ship }).ToArray();
        Ships[factorySlot] = ship;
        RecordFactoryPhase("initialized_return_before_role", ship);
        if (!factoryHost || factoryAuthorityValid?.Invoke() != true || Blocker != null)
            ship.GameEntity.DisableDynamicBodySimulation();
        bool active = ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation();
        RecordFactoryPhase("initialized_return_after_role", ship, active);
        if (active != factoryHost) throw new InvalidOperationException("factory_probe.body_role_mismatch");
        CheckFactoryAssignment();
    }

    private void FinishFactoryHull(int slot)
    {
        if (!completedFactoryHulls.Contains(Ships[slot])
            || Ships[slot].ShipsLogic.GetShipAssignment(Ships[slot].Team.TeamSide, Ships[slot].Formation.FormationIndex)?.MissionShip != Ships[slot])
            throw new InvalidOperationException("factory_probe.assignment_incomplete");
        RecordFactoryPhase("factory_return_assigned", Ships[slot]);
        CheckFactoryAssignment();
    }

    private void SetFactoryProbeAuthority(bool simulate)
    {
        if (factoryTerminal || !factoryMaterialized) return;
        CheckFactoryAssignment();
        foreach (var ship in completedFactoryHulls)
            if (!ship.GameEntity.IsValid || ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation() != factoryHost)
                throw new InvalidOperationException("factory_probe.body_role_changed");
        if (simulate != factoryHost) throw new InvalidOperationException("factory_probe.authority_mismatch");
        if (!factoryReleased && factoryHost)
            foreach (var ship in completedFactoryHulls) ship.SetAnchor(false);
        factoryReleased = true;
        Simulating = factoryHost;
    }

    internal bool CanApplyFactoryFrames()
    {
        if (!IsFactoryProbe || factoryTerminal || !factoryMaterialized || factoryHost) return false;
        CheckFactoryAssignment();
        foreach (var ship in completedFactoryHulls)
            if (!ship.GameEntity.IsValid || ship.GameEntity.HasDynamicRigidBodyAndActiveSimulation())
                throw new InvalidOperationException("factory_probe.follower_body_active_or_invalid");
        return true;
    }

    private void HoldFactoryProbe()
    {
        if (factoryTerminal) return;
        factoryTerminal = true;
        factoryObserving = false;
        Simulating = false;
        try { CancelControls(); }
        catch (Exception exception) { Reject("factory_probe.control_cleanup_failed:" + exception); }
        foreach (var ship in completedFactoryHulls)
        {
            try { if (ship.GameEntity.IsValid) ship.GameEntity.DisableDynamicBodySimulation(); }
            catch (Exception exception) { Reject("factory_probe.body_hold_failed:" + exception); }
        }
        RecordFactoryPhase("terminal_hold", null);
    }

    internal void ObserveFactoryFixedTick(NavalPhysics physics, bool parallel)
    {
        if (!IsFactoryProbe || !factoryObserving) return;
        if (parallel) Interlocked.Increment(ref factoryParallelEntries);
        else Interlocked.Increment(ref FixedTicks);
        if (!completedFactoryHulls.Any(ship => ship.Physics == physics))
        {
            Interlocked.Increment(ref factoryPreCompletionFixedEntries);
            if (!factoryHost) Reject("factory_probe.follower_fixed_entry_before_complete_or_unattributed");
            return;
        }
        if (physics.GameEntity.HasDynamicRigidBodyAndActiveSimulation())
        {
            if (parallel) Interlocked.Increment(ref factoryActiveParallelEntries);
            else Interlocked.Increment(ref ActiveFixedTicks);
            if (!factoryHost) Reject("factory_probe.follower_active_fixed_entry");
        }
    }

    internal void ObserveFactoryForce()
    {
        if (!IsFactoryProbe || !factoryObserving) return;
        Interlocked.Increment(ref ForceApplications);
        if (!factoryHost) Reject("factory_probe.follower_force_entry");
    }

    private void RecordFactoryPhase(string phase, MissionShip ship, bool? active = null)
    {
        if (factoryTrace.Count >= 16) return;
        factoryTrace.Add(new
        {
            phase, slot = factorySlot, electedHost = factoryHost, pointer = ship?.GameEntity.Pointer.ToUInt64(), active,
            fixedEntries = Interlocked.Read(ref FixedTicks), activeFixedEntries = Interlocked.Read(ref ActiveFixedTicks),
            parallelEntries = Interlocked.Read(ref factoryParallelEntries), forceEntries = Interlocked.Read(ref ForceApplications)
        });
    }

    private object InspectFactoryProbe() => !IsFactoryProbe ? null : new
    {
        factoryHost, factoryAttempted, factoryMaterialized, factoryReleased, factoryTerminal,
        preCompletionOrUnattributedFixedEntries = Interlocked.Read(ref factoryPreCompletionFixedEntries),
        parallelFixedEntries = Interlocked.Read(ref factoryParallelEntries), activeParallelFixedEntries = Interlocked.Read(ref factoryActiveParallelEntries),
        trace = factoryTrace.ToArray(), traceLimit = 16,
        unobserved = "native prefab/body creation before managed NavalPhysics callbacks; native solver and other force APIs; no parallel barrier",
        followerFrameSemantics = "SetGlobalFrame(isTeleportation:true) plus navmesh; experimental contact, no support correction"
    };
}
#endif
