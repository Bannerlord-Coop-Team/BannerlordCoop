using Common.Util;
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
    // Agents with an attached or deferred pilot seat.
    private readonly HashSet<Guid> pilotSeatAgentIds = new();
    private readonly List<Guid> pilotSeatRefreshIds = new();
    private bool refreshingPilotSeats;

    private sealed class PilotSeatAttachment
    {
        public CoopAgentInfo Info;
        public StandingPoint Point;
        public AgentPilotSeatData Data;
        public string ControllerId;

        public bool MatchesAgentAuthority(CoopAgentInfo info) =>
            ReferenceEquals(info, Info)
            && info.CurrentAuthority == ControllerId
            && info.AuthorityRevision == Data.AgentRevision;

        public bool MatchesMachineAuthority(string owner, int epoch, int revision) =>
            owner == ControllerId && epoch == Data.HostEpoch && revision == Data.MachineRevision;
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
            ClearPilotSeat(agentId, state);
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
                ReleasePilotSeat(state);
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
            ReleasePilotSeat(state);
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
        SetPilotSeat(info.AgentId, state,
            new PilotSeatAttachment { Info = info, Point = point, Data = data, ControllerId = owner });
        return true;
    }

    private void OnPilotMachineAuthorityChanged(int machineId) => RefreshPilotSeats(machineId);

    private void RefreshPilotSeats(int? machineId = null)
    {
        if (pilotSeatAgentIds.Count == 0 || refreshingPilotSeats) return;

        // Releasing a seat can change the index. A nested authority notification can wait for the next tick.
        refreshingPilotSeats = true;
        try
        {
            pilotSeatRefreshIds.AddRange(pilotSeatAgentIds);
            foreach (Guid agentId in pilotSeatRefreshIds)
            {
                if (!_agentStates.TryGetValue(agentId, out var state)) continue;
                ReleaseStalePilotSeat(agentId, state, machineId);
                RetryPendingPilotSeat(agentId, state, machineId);
            }
        }
        finally
        {
            pilotSeatRefreshIds.Clear();
            refreshingPilotSeats = false;
        }
    }

    private void ReleaseStalePilotSeat(Guid agentId, RemoteAgentActionState state, int? machineId)
    {
        var seat = state.PilotSeat;
        if (seat == null || (machineId.HasValue && seat.Data.MachineId != machineId)) return;

        // Check the agent first because reading machine authority can re-announce it after a host change.
        if (agentRegistry.TryGetAgentInfo(agentId, out var info) && info.Agent is { } agent
            && agent.Mission == Mission.Current && agent.IsActive()
            && pilotMission == Mission.Current && seat.MatchesAgentAuthority(info)
            && pilotMachines != null
            && pilotMachines.TryGetMachineAuthority(seat.Data.MachineId, out var owner, out int epoch, out int revision)
            && seat.MatchesMachineAuthority(owner, epoch, revision))
        {
            return;
        }
        ReleasePilotSeat(state);
    }

    private void RetryPendingPilotSeat(Guid agentId, RemoteAgentActionState state, int? machineId)
    {
        if (state.PendingPilotSeat is not RemoteAction pending
            || (machineId.HasValue && pending.Data.PilotSeat.Value.MachineId != machineId)) return;

        if (!agentRegistry.TryGetAgentInfo(agentId, out var info) || info.Agent is not { } agent
            || agent.Mission != Mission.Current || !agent.IsActive()
            || agentRegistry.IsLocallyControlled(agentId)
            || !IsCurrentActionAuthority(info, pending.ControllerId, pending.BattleHostEpoch))
        {
            SetPendingPilotSeat(agentId, state, null);
            return;
        }
        if (!TryApplyPilotSeat(info, pending, out _)) return;
        if (state.PilotSeat != null && state.PilotSeat.ControllerId == pending.ControllerId &&
            state.PilotSeat.Data.Equals(pending.Data.PilotSeat.Value))
        {
            // Native use transitions may change the hands after the action was first applied.
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
        UpdatePilotSeatIndex(agentId, state);
    }

    private void SetPilotSeat(Guid agentId, RemoteAgentActionState state, PilotSeatAttachment attachment)
    {
        state.PilotSeat = attachment;
        UpdatePilotSeatIndex(agentId, state);
    }

    private void UpdatePilotSeatIndex(Guid agentId, RemoteAgentActionState state)
    {
        if (state.PilotSeat != null || state.PendingPilotSeat.HasValue)
            pilotSeatAgentIds.Add(agentId);
        else
            pilotSeatAgentIds.Remove(agentId);
    }

    private void ClearPilotSeat(Guid agentId, RemoteAgentActionState state)
    {
        SetPendingPilotSeat(agentId, state, null);
        ReleasePilotSeat(state);
    }

    private void ReleasePilotSeat(RemoteAgentActionState state)
    {
        var seat = state.PilotSeat;
        if (seat == null) return;
        SetPilotSeat(seat.Info.AgentId, state, null);
        if (seat.Info.ReplicatedPilotPoint == seat.Point) seat.Info.ReplicatedPilotPoint = null;
        Agent agent = seat.Info.Agent;
        if (agent == null || agent.Mission != Mission.Current || !agent.IsActive() ||
            agentRegistry.IsLocallyControlled(seat.Info.AgentId) ||
            agent.CurrentlyUsedGameObject != seat.Point || seat.Point.UserAgent != agent) return;
        using (new AllowedThread()) agent.StopUsingGameObject();
    }
}
