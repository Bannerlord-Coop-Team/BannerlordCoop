using Common.Messaging;
using Common;
using Missions.Agents.Messages;
using Missions.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public partial class SiegeMachineStateReplicator
{
    private readonly Dictionary<Guid, NetworkMangonelAmmoPickup> localMangonelPickups = new();
    private readonly Dictionary<Guid, NetworkMangonelAmmoPickup> pendingMangonelPickups = new();
    private readonly Dictionary<Guid, NetworkMangonelAmmoPickup> decidedMangonelPickups = new();

    private void ClearMangonelPickups()
    {
        localMangonelPickups.Clear();
        pendingMangonelPickups.Clear();
        decidedMangonelPickups.Clear();
    }

    private void HandleMangonelAmmoPickup(MessagePayload<MangonelAmmoPickup> payload)
    {
        if (mangonelLoadsDisposed || Mission.Current == null) return;
        RefreshMachineCache();
        var agent = payload.What.Agent;
        var machine = payload.What.Machine;
        if (agent == null || machine == null || !agent.IsActive() || agent.Mission != Mission.Current ||
            !agentRegistry.IsLocallyControlled(agent) || !agentRegistry.TryGetAgentInfo(agent, out var info) ||
            info.CurrentAuthority != session.OwnControllerId ||
            !machinesById.TryGetValue(machine.Id.Id, out var current) || !ReferenceEquals(machine, current) ||
            !agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty ||
            !(agent.CurrentlyUsedGameObject is StandingPoint point) ||
            !machine.AmmoPickUpPoints.Contains(point) || string.IsNullOrEmpty(session.InstanceId) ||
            string.IsNullOrEmpty(session.HostControllerId) || session.HostEpoch <= 0 ||
            localMangonelPickups.ContainsKey(info.AgentId)) return;
        var request = new NetworkMangonelAmmoPickup(session.InstanceId, Guid.NewGuid(), info.AgentId,
            info.CurrentAuthority, info.AuthorityRevision, info.SiegeEquipmentGrantRevision, machine.Id.Id,
            point.Id.Id, session.HostControllerId, session.HostEpoch);
        localMangonelPickups.Add(info.AgentId, request);
        if (session.IsLocalHost) ApplyMangonelPickup(request);
        else network.Send(session.HostControllerId, request);
    }

    private void HandleNetworkMangonelAmmoPickup(MessagePayload<NetworkMangonelAmmoPickup> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (mangonelLoadsDisposed || Mission.Current == null) return;
            RefreshMachineCache();
            ApplyMangonelPickup(payload.What);
        });
    }

    private void TickMangonelPickups()
    {
        foreach (var request in new List<NetworkMangonelAmmoPickup>(pendingMangonelPickups.Values))
        {
            pendingMangonelPickups.Remove(request.AgentId);
            ApplyMangonelPickup(request);
        }
        foreach (var request in new List<NetworkMangonelAmmoPickup>(localMangonelPickups.Values))
        {
            if (request.HostEpoch != session.HostEpoch || request.HostControllerId != session.HostControllerId)
                localMangonelPickups.Remove(request.AgentId);
        }
    }

    internal void ApplyMangonelPickup(NetworkMangonelAmmoPickup request)
    {
        if (mangonelLoadsDisposed || Mission.Current == null || request == null ||
            request.RequestId == Guid.Empty || request.AgentId == Guid.Empty || request.GrantRevision < 0 ||
            string.IsNullOrEmpty(session.InstanceId) || request.BattleId != session.InstanceId ||
            string.IsNullOrEmpty(request.AgentOwner) || !Enum.IsDefined(typeof(MangonelPickupPhase), request.Phase) ||
            request.HostEpoch <= 0 || request.HostEpoch != session.HostEpoch ||
            request.HostControllerId != session.HostControllerId) return;

        if (!machinesById.TryGetValue(request.MachineId, out var usable) ||
            !agentRegistry.TryGetAgentInfo(request.AgentId, out var info) || request.AgentRevision > info.AuthorityRevision ||
            (request.Phase == MangonelPickupPhase.Request && request.AgentRevision == info.AuthorityRevision &&
             request.GrantRevision > info.SiegeEquipmentGrantRevision))
        {
            // Registration and owner migration are applied on this same game-thread queue.
            if (pendingMangonelPickups.TryGetValue(request.AgentId, out var pending) &&
                (pending.AgentRevision > request.AgentRevision ||
                 (pending.AgentRevision == request.AgentRevision && pending.GrantRevision > request.GrantRevision))) return;
            if (pendingMangonelPickups.Count < 64 || pendingMangonelPickups.ContainsKey(request.AgentId))
                pendingMangonelPickups[request.AgentId] = request;
            return;
        }
        if (!(usable is Mangonel machine) || machine.StartingAmmoCount <= 0 ||
            !machine.AmmoPickUpPoints.Any(p => p.Id.Id == request.PointId)) return;

        if (request.Phase != MangonelPickupPhase.Request)
        {
            if (request.RemainingAmmo < 0) return;
            ApplyMangonelSupply(machine, request.RemainingAmmo);
            if (!localMangonelPickups.TryGetValue(request.AgentId, out var local) ||
                local.RequestId != request.RequestId) return;
            localMangonelPickups.Remove(request.AgentId);
            if (request.Phase != MangonelPickupPhase.Granted || !IsCurrentPicker(request, info) ||
                !agentRegistry.IsLocallyControlled(info.Agent) ||
                info.SiegeEquipmentGrantRevision != request.GrantRevision ||
                !info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty) return;
            var weapon = new MissionWeapon(machine.OriginalMissileItem, null, null, 1);
            info.Agent.EquipWeaponToExtraSlotAndWield(ref weapon);
            // A remote approval arrives after vanilla has released the pickup standing point.
            if (!info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty &&
                info.SiegeEquipmentGrantRevision == request.GrantRevision)
                messageBroker.Publish(this, new LadderForkGranted(info.Agent));
            return;
        }

        if (!session.IsLocalHost || !IsCurrentPicker(request, info)) return;
        if (decidedMangonelPickups.TryGetValue(request.AgentId, out var previous))
        {
            if (previous.RequestId == request.RequestId)
            {
                network.Send(request.AgentOwner, previous);
                return;
            }
            if (previous.AgentRevision > request.AgentRevision ||
                (previous.AgentRevision == request.AgentRevision &&
                 (previous.GrantRevision > request.GrantRevision ||
                  (previous.GrantRevision == request.GrantRevision && previous.Phase == MangonelPickupPhase.Granted))))
                return;
        }
        // Shared ammunition stays host-owned like damage, independently of pilot simulation claims.
        bool granted = machine.AmmoCount > 0 && info.SiegeEquipmentGrantRevision == request.GrantRevision &&
            info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty;
        if (granted) machine.ConsumeAmmo();
        var decision = request.Decide(granted, machine.AmmoCount);
        decidedMangonelPickups[request.AgentId] = decision;
        network.SendAll(decision);
        ApplyMangonelPickup(decision);
    }

    private static bool IsCurrentPicker(NetworkMangonelAmmoPickup request, CoopAgentInfo info) =>
        info.Agent != null && info.Agent.Mission == Mission.Current && info.Agent.IsActive() &&
        info.CurrentAuthority == request.AgentOwner && info.AuthorityRevision == request.AgentRevision;

    private static void ApplyMangonelSupply(Mangonel machine, int remaining)
    {
        // A delayed snapshot cannot replenish a finite supply already exhausted by an approval.
        if (remaining >= 0 && machine.StartingAmmoCount > 0 && remaining < machine.AmmoCount)
            machine.SetAmmo(remaining);
    }
}
