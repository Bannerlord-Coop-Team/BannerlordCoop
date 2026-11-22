using Common.Util;
using Common.BattleBenchmark;
using Missions.Agents.Packets;
using Missions.Battles;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

public partial class RemoteAgentActionProcessor
{
    private string pilotBattleId;
    private Mission pilotMission;
    private ISiegeMachineStateReplicator pilotMachines;
    private readonly HashSet<Guid> pilotSeatAgentIds = new();
    private readonly List<Guid> pilotSeatRefreshIds = new();

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
        ClearPilotSeats();
        pilotBattleId = battleId;
        pilotMission = machineState == null ? null : Mission.Current;
        pilotMachines = machineState;
        if (pilotMachines != null) pilotMachines.AuthorityChanged += OnPilotMachineAuthorityChanged;
    }

    private void ClearPilotSeats()
    {
        foreach (Guid agentId in pilotSeatAgentIds.ToArray())
        {
            if (!_agentStates.TryGetValue(agentId, out var state)) continue;
            SetPendingPilotSeat(agentId, state, null);
            ReleasePilotSeat(state, preserveLocalUser: true);
            RemoveAgentStateIfEmpty(agentId, state);
        }
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
        pilotSeatAgentIds.Add(info.AgentId);
        return true;
    }

    private void OnPilotMachineAuthorityChanged(int machineId) => RefreshPilotSeats(machineId);

    private void RefreshPilotSeats(int? machineId = null)
    {
        using var measurement = battleBenchmark.Measure(BattleBenchmarkSection.RemoteActionPilot);
        int registryLookups = 0;
        if (pilotSeatAgentIds.Count == 0 || pilotSeatRefreshIds.Count != 0) return;

        // Releasing a seat can change the index. A nested authority notification can wait for the next tick.
        pilotSeatRefreshIds.AddRange(pilotSeatAgentIds);
        try
        {
            foreach (Guid agentId in pilotSeatRefreshIds)
            {
                if (_agentStates.TryGetValue(agentId, out var state))
                    RefreshPilotSeat(agentId, state, machineId, ref registryLookups);
            }
        }
        finally
        {
            battleBenchmark.RecordRemoteActionPilotWork(pilotSeatRefreshIds.Count, registryLookups);
            pilotSeatRefreshIds.Clear();
        }
    }

    private void RefreshPilotSeat(Guid agentId, RemoteAgentActionState state, int? machineId,
        ref int registryLookups)
    {
        var seat = state.PilotSeat;
        RemoteAction? deferred = state.PendingPilotSeat;
        bool refreshAttachment = seat != null && (!machineId.HasValue || seat.Data.MachineId == machineId);
        bool retryPending = deferred.HasValue && (!machineId.HasValue || deferred.Value.Data.PilotSeat.Value.MachineId == machineId);
        if (!refreshAttachment && !retryPending) return;

        registryLookups++;
        agentRegistry.TryGetAgentInfo(agentId, out var info);
        Agent agent = info?.Agent;
        bool active = agent != null && agent.Mission == Mission.Current && agent.IsActive();
        if (refreshAttachment && (!active || pilotMission != Mission.Current || !ReferenceEquals(info, seat.Info) ||
            info.CurrentAuthority != seat.ControllerId || info.AuthorityRevision != seat.Data.AgentRevision ||
            pilotMachines == null ||
            !pilotMachines.TryGetMachineAuthority(seat.Data.MachineId, out var owner, out int epoch, out int revision) ||
            owner != seat.ControllerId || epoch != seat.Data.HostEpoch || revision != seat.Data.MachineRevision))
        {
            ReleasePilotSeat(state, preserveLocalUser: true);
        }

        if (!retryPending) return;
        RemoteAction pending = deferred.Value;
        if (!active || info.CurrentAuthority == controllerIdProvider.ControllerId ||
            !IsCurrentActionAuthority(info, pending.ControllerId, pending.BattleHostEpoch))
        {
            SetPendingPilotSeat(agentId, state, null);
            return;
        }
        if (!TryApplyPilotSeat(info, pending, out _)) return;
        if (state.PilotSeat != null && state.PilotSeat.ControllerId == pending.ControllerId &&
            state.PilotSeat.Data.Equals(pending.Data.PilotSeat.Value))
        {
            // Native use transitions may change the hands after the action was first applied.
            using (battleBenchmark.Measure(BattleBenchmarkSection.RemoteActionApply))
            using (new AllowedThread())
            {
                if (pending.Data.Equipment.HasValue &&
                    !pending.Data.Equipment.Value.TryApplyForAction(agent)) return;
                pending.Data.ApplyActionChannels(agent, agentVisualActionAccessor);
            }
        }
        SetPendingPilotSeat(agentId, state, null);
    }

    private void SetPendingPilotSeat(Guid agentId, RemoteAgentActionState state, RemoteAction? action)
    {
        if (!action.HasValue && !state.PendingPilotSeat.HasValue) return;
        state.PendingPilotSeat = action;
        if (action.HasValue)
            pilotSeatAgentIds.Add(agentId);
        else if (state.PilotSeat == null)
            pilotSeatAgentIds.Remove(agentId);
    }

    private void ReleasePilotSeat(RemoteAgentActionState state, bool preserveLocalUser)
    {
        var seat = state.PilotSeat;
        if (seat == null) return;
        state.PilotSeat = null;
        if (!state.PendingPilotSeat.HasValue) pilotSeatAgentIds.Remove(seat.Info.AgentId);
        if (seat.Info.ReplicatedPilotPoint == seat.Point) seat.Info.ReplicatedPilotPoint = null;
        Agent agent = seat.Info.Agent;
        if (agent == null || agent.Mission != Mission.Current || !agent.IsActive() ||
            (preserveLocalUser && agentRegistry.IsLocallyControlled(seat.Info.AgentId)) ||
            agent.CurrentlyUsedGameObject != seat.Point || seat.Point.UserAgent != agent) return;
        using (new AllowedThread()) agent.StopUsingGameObject();
    }
}
