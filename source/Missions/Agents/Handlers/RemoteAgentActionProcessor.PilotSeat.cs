using Common.Util;
using Missions.Agents.Packets;
using Missions.Battles;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public partial class RemoteAgentActionProcessor
{
    private string pilotBattleId;
    private Mission pilotMission;
    private ISiegeMachineStateReplicator pilotMachines;

    private sealed class PilotSeatAttachment
    {
        public CoopAgentInfo Info;
        public StandingPoint Point;
        public AgentPilotSeatData Data;
        public string ControllerId;
    }

    public void BindPilotSeats(string battleId, ISiegeMachineStateReplicator machineState)
    {
        if (pilotMachines != null) pilotMachines.AuthorityChanged -= OnPilotMachineAuthorityChanged;
        foreach (var state in _agentStates.Values)
        {
            state.PendingPilotSeat = null;
            ReleasePilotSeat(state, preserveLocalUser: true);
        }
        pilotBattleId = battleId;
        pilotMission = machineState == null ? null : Mission.Current;
        pilotMachines = machineState;
        if (pilotMachines != null) pilotMachines.AuthorityChanged += OnPilotMachineAuthorityChanged;
    }

    public AgentPilotSeatData? CapturePilotSeat(CoopAgentInfo info, AgentPilotSeatData? previous)
    {
        if (pilotMachines == null || pilotMission != Mission.Current || info.Agent?.Mission != pilotMission)
            return null;

        Agent agent = info.Agent;
        if (agent.Controller == AgentControllerType.Player && agent.CurrentlyUsedGameObject is StandingPoint point)
        {
            var candidates = pilotMission.MissionObjects.OfType<RangedSiegeWeapon>()
                .Where(m => ReferenceEquals(m.PilotStandingPoint, point) &&
                    !ReferenceEquals(m.LoadAmmoStandingPoint, point)).Take(2).ToArray();
            if (candidates.Length == 1)
            {
                var machine = candidates[0];
                if (ResolvePilotPoint(machine.Id.Id, point.Id.Id) == point && point.UserAgent == agent &&
                    pilotMachines.TryGetMachineAuthority(machine.Id.Id, out var owner, out int epoch, out int revision) &&
                    owner == controllerIdProvider.ControllerId && owner == info.CurrentAuthority)
                {
                    return new AgentPilotSeatData(pilotBattleId, machine.Id.Id, point.Id.Id,
                        epoch, revision, info.AuthorityRevision, true);
                }
            }
        }

        return previous.HasValue && previous.Value.BattleId == pilotBattleId
            ? previous.Value.Stopped(info.AuthorityRevision) : (AgentPilotSeatData?)null;
    }

    private StandingPoint ResolvePilotPoint(int machineId, int pointId)
    {
        if (pilotMission == null || pilotMission != Mission.Current || machineId <= 0 || pointId <= 0)
            return null;
        var machines = pilotMission.MissionObjects.OfType<UsableMachine>()
            .Where(m => m.Id.Id == machineId).Take(2).ToArray();
        if (machines.Length != 1 || !(machines[0] is RangedSiegeWeapon weapon) ||
            weapon.PilotStandingPoint == null || weapon.StandingPoints == null ||
            weapon.PilotStandingPoint.Id.Id != pointId ||
            ReferenceEquals(weapon.PilotStandingPoint, weapon.LoadAmmoStandingPoint)) return null;
        var points = weapon.StandingPoints.Where(p => p.Id.Id == pointId).Take(2).ToArray();
        return points.Length == 1 && ReferenceEquals(points[0], weapon.PilotStandingPoint) ? points[0] : null;
    }

    private bool TryApplyPilotSeat(CoopAgentInfo info, RemoteAction action, out bool useConflict)
    {
        useConflict = false;
        if (!action.Data.PilotSeat.HasValue) return true;
        AgentPilotSeatData data = action.Data.PilotSeat.Value;
        if (pilotMachines == null || pilotMission != Mission.Current) return false;
        if (data.BattleId != pilotBattleId ||
            data.AgentRevision < info.AuthorityRevision ||
            data.HostEpoch <= 0 || data.MachineRevision < 0 || data.MachineId <= 0 || data.PointId <= 0)
            return true;
        if (data.AgentRevision > info.AuthorityRevision || info.CurrentAuthority != action.ControllerId) return false;

        var state = GetOrCreateAgentState(info.AgentId);
        if (!data.Using)
        {
            // A stop can follow handback, but cannot release another seat or a newer occupant.
            if (state.PilotSeat != null && state.PilotSeat.Data.MachineId == data.MachineId &&
                state.PilotSeat.Data.PointId == data.PointId &&
                state.PilotSeat.Data.HostEpoch == data.HostEpoch &&
                state.PilotSeat.Data.MachineRevision == data.MachineRevision)
                ReleasePilotSeat(state, preserveLocalUser: true);
            return true;
        }

        if (!pilotMachines.TryGetMachineAuthority(data.MachineId, out var owner, out int epoch, out int revision))
            return false;
        int order = data.HostEpoch.CompareTo(epoch);
        if (order == 0) order = data.MachineRevision.CompareTo(revision);
        if (order > 0) return false;
        if (order < 0 || owner != action.ControllerId) return true;
        StandingPoint point = ResolvePilotPoint(data.MachineId, data.PointId);
        if (point == null) return false;
        Agent agent = info.Agent;
        if ((point.UserAgent != null && point.UserAgent != agent) ||
            (point.MovingAgent != null && point.MovingAgent != agent))
        {
            useConflict = true;
            return false;
        }
        if (state.PilotSeat != null && state.PilotSeat.Point != point)
            ReleasePilotSeat(state, preserveLocalUser: true);
        // In particular, do not stop the separate Mangonel load transaction.
        if (agent.CurrentlyUsedGameObject != null && agent.CurrentlyUsedGameObject != point)
        {
            useConflict = true;
            return false;
        }
        if (agent.CurrentlyUsedGameObject == null)
        {
            using (new AllowedThread()) agent.UseGameObject(point);
        }
        if (agent.CurrentlyUsedGameObject != point || point.UserAgent != agent) return false;
        info.ReplicatedPilotPoint = point;
        state.PilotSeat = new PilotSeatAttachment { Info = info, Point = point, Data = data, ControllerId = owner };
        return true;
    }

    private void OnPilotMachineAuthorityChanged(int machineId) => RefreshPilotSeats();

    private void RefreshPilotSeats()
    {
        foreach (var entry in _agentStates)
        {
            var state = entry.Value;
            var seat = state.PilotSeat;
            if (seat != null)
            {
                Agent agent = seat.Info.Agent;
                if (pilotMission != Mission.Current || agent.Mission != Mission.Current || !agent.IsActive() ||
                    !agentRegistry.TryGetAgentInfo(seat.Info.AgentId, out var current) || !ReferenceEquals(current, seat.Info) ||
                    current.CurrentAuthority != seat.ControllerId || current.AuthorityRevision != seat.Data.AgentRevision ||
                    pilotMachines == null ||
                    !pilotMachines.TryGetMachineAuthority(seat.Data.MachineId, out var owner, out int epoch, out int revision) ||
                    owner != seat.ControllerId || epoch != seat.Data.HostEpoch || revision != seat.Data.MachineRevision)
                    ReleasePilotSeat(state, preserveLocalUser: true);
            }
            if (!state.PendingPilotSeat.HasValue) continue;
            RemoteAction pending = state.PendingPilotSeat.Value;
            if (!agentRegistry.TryGetAgentInfo(entry.Key, out var info) ||
                agentRegistry.IsLocallyControlled(entry.Key) || info.Agent == null ||
                info.Agent.Mission != Mission.Current || !info.Agent.IsActive() ||
                !IsCurrentActionAuthority(info, pending.ControllerId, pending.BattleHostEpoch))
            {
                state.PendingPilotSeat = null;
                continue;
            }
            if (!TryApplyPilotSeat(info, pending, out _)) continue;
            if (state.PilotSeat != null && state.PilotSeat.ControllerId == pending.ControllerId &&
                state.PilotSeat.Data.Equals(pending.Data.PilotSeat.Value))
            {
                // Native use transitions may change the hands after the action was first applied.
                using (new AllowedThread())
                {
                    if (pending.Data.Equipment.HasValue &&
                        !pending.Data.Equipment.Value.TryApplyForAction(info.Agent)) continue;
                    pending.Data.ApplyActionChannels(info.Agent, agentVisualActionAccessor);
                }
            }
            state.PendingPilotSeat = null;
        }
    }

    private void ReleasePilotSeat(RemoteAgentActionState state, bool preserveLocalUser)
    {
        var seat = state.PilotSeat;
        if (seat == null) return;
        state.PilotSeat = null;
        if (seat.Info.ReplicatedPilotPoint == seat.Point) seat.Info.ReplicatedPilotPoint = null;
        Agent agent = seat.Info.Agent;
        if (agent == null || agent.Mission != Mission.Current || !agent.IsActive() ||
            (preserveLocalUser && agentRegistry.IsLocallyControlled(seat.Info.AgentId)) ||
            agent.CurrentlyUsedGameObject != seat.Point || seat.Point.UserAgent != agent) return;
        using (new AllowedThread()) agent.StopUsingGameObject();
    }
}
