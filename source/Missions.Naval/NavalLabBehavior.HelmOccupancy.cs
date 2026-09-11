#if DEBUG
using System;
using System.Linq;
using Common;
using Missions.Battles;
using Missions.Messages;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal sealed partial class NavalLabBehavior
{
    internal Action<NetworkNavalLabHelmOccupancy> SendHelmOccupancy;
    internal Action<Agent> ForgetHelmMovement;
    private readonly NetworkNavalLabHelmOccupancy[] replicatedHelms = new NetworkNavalLabHelmOccupancy[2];
    private readonly long[] observedHelmRevisions = new long[2];
    private readonly long[] confirmedHelmRevisions = new long[2];
    private readonly long[] helmApplyTicks = new long[2];
    private readonly double[] helmObservationDeadlines = new double[2];
    private NetworkNavalLabHelmOccupancy offeredHelm;
    private double helmOfferDeadline;
    private bool HelmReplicasReady => replicatedHelms.All(state => state != null)
        && Enumerable.Range(0, 2).All(slot => confirmedHelmRevisions[slot] == replicatedHelms[slot].Revision)
        && offeredHelm != null && confirmedHelmRevisions[OwnSlot] == offeredHelm.Revision;

    private ShipControllerMachine ReplicatedHelmMachine(int slot)
    {
        if (!GameThread.Instance.IsGameThread || !IsTwoClientNative || Mission == null || Mission != Mission.Current
            || !CanUseNativeControls || factoryTerminal || nativeTerminalHold || Blocker != null
            || !nativeDeploymentComplete || nativeDeploymentCallbacks != 1 || nativeAfterDeploymentCallbacks != 1
            || !Mission.IsDeploymentFinished || Mission.Mode != MissionMode.Battle || GameNetwork.IsClientOrReplay
            || slot < 0 || slot >= Ships.Length)
            throw new InvalidOperationException("native.helm_replica_lifecycle");
        var ship = Ships[slot];
        var machine = ship?.ShipControllerMachine;
        var agent = Agents[slot * NavalLabManifest.CrewPerShip];
        var point = machine?.PilotStandingPoint;
        if (ship == null || ship.ShipOrigin is not NavalLabShipOrigin || ship.ShipOrigin.Hull != hull
            || !ship.IsDeployed || !ship.IsInitialized || !ship.GameEntity.IsValid || machine == null
            || machine.AttachedShip != ship || !machine.GameEntity.IsValid || point == null || !point.GameEntity.IsValid
            || point.IsDeactivated || point.GetComponent<ResetAnimationOnStopUsageComponent>() == null
            || machine._navalShipsLogic != Mission.GetMissionBehavior<NavalShipsLogic>() || machine._navalShipsLogic == null
            || machine._navalAgentsLogic != Mission.GetMissionBehavior<NavalAgentsLogic>() || machine._navalAgentsLogic == null
            || agent == null || agent.Pointer == UIntPtr.Zero || agent.Mission != Mission || !agent.IsActive()
            || !agent.IsHuman || agent.HasMount || agent.IsMount || agent.Formation != ship.Formation
            || ship.Captain != agent || (slot == OwnSlot
                ? agent != Mission.MainAgent || agent != Mission.InitialPlayerAgent || !agent.IsPlayerControlled
                : agent == Mission.MainAgent || agent == Mission.InitialPlayerAgent || agent.Controller != AgentControllerType.None))
            throw new InvalidOperationException("native.helm_replica_identity");
        return machine;
    }

    private bool ObserveHelmState(int slot, ShipControllerMachine machine)
    {
        var agent = Agents[slot * NavalLabManifest.CrewPerShip];
        var point = machine.PilotStandingPoint;
        if (point.UserAgent == agent && agent.CurrentlyUsedGameObject == point && machine.PilotAgent == agent) return true;
        if (point.UserAgent == null && agent.CurrentlyUsedGameObject == null && machine.PilotAgent == null) return false;
        throw new InvalidOperationException("native.helm_partial_or_foreign_identity");
    }

    internal long HelmMovementRevision(Guid combatantId, Agent agent)
    {
        int index = Array.IndexOf(manifest.Combatants, combatantId);
        if (index < 0 || index % NavalLabManifest.CrewPerShip != 0 || Agents[index] != agent) return -1;
        int slot = index / NavalLabManifest.CrewPerShip;
        try
        {
            var machine = ReplicatedHelmMachine(slot);
            var state = slot == OwnSlot ? offeredHelm : replicatedHelms[slot];
            if (ObserveHelmState(slot, machine) || state?.Occupied == true) return -1;
            if (slot != OwnSlot && state != null && observedHelmRevisions[slot] != state.Revision) return -1;
            return state?.Revision ?? 0;
        }
        catch { return -1; }
    }

    internal void ApplyHelmOccupancy(NetworkNavalLabHelmOccupancy value)
    {
        var machine = ReplicatedHelmMachine(value.Ship);
        int slot = value.Ship;
        if (value.IncarnationId != manifest.IncarnationId || value.Epoch != 1
            || value.ShipId != manifest.Ships[slot] || value.CombatantId != manifest.Combatants[slot * NavalLabManifest.CrewPerShip]
            || value.Key != StationKey(machine, Ships[slot]) || value.Revision <= 0)
            throw new InvalidOperationException("native.helm_replica_message_identity");
        var prior = replicatedHelms[slot];
        if (prior != null && value.Revision < prior.Revision) return;
        if (value.Phase == "confirmed")
        {
            if (!value.SameState(prior) || observedHelmRevisions[slot] != value.Revision)
                throw new InvalidOperationException("native.helm_unobserved_confirmation");
            confirmedHelmRevisions[slot] = value.Revision;
            return;
        }
        if (value.Phase != "commit") throw new InvalidOperationException("native.helm_replica_phase");
        if (prior != null && value.Revision == prior.Revision)
        {
            if (!value.SameState(prior)) throw new InvalidOperationException("native.helm_replica_conflict");
            return;
        }
        if (value.Revision != (prior?.Revision ?? 0) + 1 || value.Occupied == (prior?.Occupied ?? false)
            || (prior != null && observedHelmRevisions[slot] != prior.Revision))
            throw new InvalidOperationException("native.helm_replica_order");
        bool occupied = ObserveHelmState(slot, machine);
        if (slot == OwnSlot)
        {
            if (!value.SameState(offeredHelm) || occupied != value.Occupied)
                throw new InvalidOperationException("native.helm_owner_changed_before_commit");
        }
        else if (occupied != (prior?.Occupied ?? false))
            throw new InvalidOperationException("native.helm_remote_changed_before_commit");
        replicatedHelms[slot] = value;
        helmApplyTicks[slot] = nativeHelmTicks;
        helmObservationDeadlines[slot] = ControlNow + 2;
        if (slot == OwnSlot) return;
        var agent = Agents[slot * NavalLabManifest.CrewPerShip];
        var point = machine.PilotStandingPoint;
        // Forget only managed interpolation, before native use installs the new station target.
        ForgetHelmMovement(agent);
        if (value.Occupied)
        {
            // Replicated owner use is not local interaction permission; opponent points are AIOnly here.
            if (point.MovingAgent != null || point.HasAIMovingTo || !point.LockUserFrames
                || !agent.IsAbleToUseMachine() || machine.IsAttachedShipVacant())
                throw new InvalidOperationException("native.helm_remote_not_vacant_or_eligible");
            agent.UseGameObject(point);
            if (!ObserveHelmState(slot, machine)) throw new InvalidOperationException("native.helm_remote_use_missing");
            machine.OnPilotAssignedDuringSpawn();
        }
        else agent.StopUsingGameObject();
    }

    private void TickHelmOccupancy()
    {
        if (!IsTwoClientNative || !CanUseNativeControls || factoryTerminal || Blocker != null) return;
        try
        {
            for (int slot = 0; slot < 2; slot++)
            {
                var state = replicatedHelms[slot];
                if (state == null || nativeHelmTicks <= helmApplyTicks[slot]) continue;
                bool occupied = ObserveHelmState(slot, ReplicatedHelmMachine(slot));
                if (observedHelmRevisions[slot] != state.Revision)
                {
                    if (ControlNow >= helmObservationDeadlines[slot] || occupied != state.Occupied)
                        throw new InvalidOperationException("native.helm_replica_not_observed");
                    observedHelmRevisions[slot] = state.Revision;
                    SendHelmOccupancy(state.WithPhase("ack"));
                }
                else if (slot != OwnSlot && occupied != state.Occupied)
                    throw new InvalidOperationException("native.helm_remote_occupancy_lost");
                if (confirmedHelmRevisions[slot] != state.Revision && ControlNow >= helmObservationDeadlines[slot])
                    throw new InvalidOperationException("native.helm_confirmation_timeout");
            }
            if (!nativeAutoHelmObserved || nativeHelmPhase == "pending" || nativeHelmPhase == "failed") return;
            if (offeredHelm != null && confirmedHelmRevisions[OwnSlot] != offeredHelm.Revision)
            {
                if (ControlNow >= helmOfferDeadline) throw new InvalidOperationException("native.helm_offer_timeout");
                return;
            }
            var localMachine = ReplicatedHelmMachine(OwnSlot);
            bool localOccupied = ObserveHelmState(OwnSlot, localMachine);
            if (localOccupied == (offeredHelm?.Occupied ?? false)) return;
            offeredHelm = new NetworkNavalLabHelmOccupancy(manifest.IncarnationId, 1, OwnSlot, manifest.Ships[OwnSlot],
                manifest.Combatants[OwnSlot * NavalLabManifest.CrewPerShip], StationKey(localMachine, LocalShip),
                checked((offeredHelm?.Revision ?? 0) + 1), localOccupied, "offer");
            helmOfferDeadline = ControlNow + 2;
            SendHelmOccupancy(offeredHelm);
        }
        catch (Exception exception) { FailNativeHelm("replication:" + exception.Message); }
    }

    private void RefreshReplicatedFollowerHelmTarget()
    {
        if (factoryHost || !IsTwoClientNative || factoryTerminal || Blocker != null) return;
        int slot = 1 - OwnSlot;
        var state = replicatedHelms[slot];
        if (state?.Occupied != true || observedHelmRevisions[slot] != state.Revision) return;
        try
        {
            var machine = ReplicatedHelmMachine(slot);
            var agent = Agents[slot * NavalLabManifest.CrewPerShip];
            var point = machine.PilotStandingPoint;
            if (!ObserveHelmState(slot, machine) || !point.LockUserFrames)
                throw new InvalidOperationException("native.helm_remote_target_identity");
            var frame = point.GetUserFrameForAgent(agent);
            agent.SetTargetPositionAndDirection(frame.Origin.AsVec2, in frame.Rotation.f);
        }
        catch (Exception exception) { FailNativeHelm("replication:" + exception.Message); }
    }

    private object InspectHelmOccupancy() => new
    {
        ready = HelmReplicasReady, ownerRevision = offeredHelm?.Revision ?? 0,
        replicas = replicatedHelms.Select((state, slot) => new
        {
            slot, state, observedRevision = observedHelmRevisions[slot], confirmedRevision = confirmedHelmRevisions[slot],
            identity = InspectHelmIdentity(slot < Ships.Length ? Ships[slot] : null, slot)
        }).ToArray()
    };
}
#endif
