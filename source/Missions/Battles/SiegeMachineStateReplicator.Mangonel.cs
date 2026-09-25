using Common;
using Common.Messaging;
using Common.Util;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public partial class SiegeMachineStateReplicator
{
    private readonly Dictionary<Guid, NetworkMangonelLoad> mangonelLoads = new();
    private readonly HashSet<Guid> finishedMangonelLoads = new();
    private readonly HashSet<Guid> animatedMangonelLoads = new();
    private readonly HashSet<Guid> begunMangonelLoadActions = new();
    private readonly HashSet<Guid> readyMangonelLoads = new();
    private readonly Dictionary<Guid, NetworkMangonelLoad> consumedMangonelLoads = new();
    private readonly Dictionary<Guid, NetworkMangonelLoad> acceptedMangonelLoads = new();
    private readonly Dictionary<Guid, NetworkMangonelLoad> pendingMangonelLoads = new();
    private bool mangonelLoadsDisposed;

    private bool IsCurrentLoad(NetworkMangonelLoad load, out CoopAgentInfo info, out Mangonel machine)
    {
        return TryResolveMangonelLoad(load, out info, out machine) &&
            TryGetMachineAuthority(load.MachineId, out var owner, out int epoch, out int revision) &&
            owner == load.Simulator && epoch == load.HostEpoch && revision == load.MachineRevision;
    }

    private bool TryResolveMangonelLoad(NetworkMangonelLoad load, out CoopAgentInfo info, out Mangonel machine)
    {
        info = null;
        machine = null;
        if (mangonelLoadsDisposed || load == null || load.RequestId == Guid.Empty || load.GrantId == Guid.Empty ||
            !Enum.IsDefined(typeof(MangonelLoadPhase), load.Phase) ||
            (!(load.Phase == MangonelLoadPhase.Cancel && load.SenderControllerId == load.Simulator) &&
            load.SenderControllerId != ((load.Phase == MangonelLoadPhase.Request || load.Phase == MangonelLoadPhase.Cancel ||
                load.Phase == MangonelLoadPhase.Ready || load.Phase == MangonelLoadPhase.OwnerConsumed)
                ? load.AgentOwner : load.Simulator)) ||
            string.IsNullOrEmpty(session.InstanceId) || load.BattleId != session.InstanceId ||
            Mission.Current == null || !agentRegistry.TryGetAgentInfo(load.AgentId, out info) ||
            info.Agent == null || info.Agent.Mission != Mission.Current || !info.Agent.IsActive() ||
            info.CurrentAuthority != load.AgentOwner || info.AuthorityRevision != load.AgentRevision ||
            !machinesById.TryGetValue(load.MachineId, out var usable) || !(usable is Mangonel mangonel) ||
            mangonel.LoadAmmoStandingPoint == null || mangonel.LoadAmmoStandingPoint.Id.Id != load.PointId)
            return false;
        machine = mangonel;
        return true;
    }

    private static bool HoldsLoadMissile(CoopAgentInfo info, Mangonel machine, Guid grantId)
    {
        var weapon = info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot];
        return info.SiegeEquipmentGrant == grantId && !info.IsSiegeGrantConsumed(grantId) &&
            !weapon.IsEmpty && ReferenceEquals(weapon.Item, machine.OriginalMissileItem) && weapon.Amount == 1 &&
            info.Agent.GetPrimaryWieldedItemIndex() == EquipmentIndex.ExtraWeaponSlot;
    }

    private void ObserveMangonelLoadActions()
    {
        foreach (var load in mangonelLoads.Values)
        {
            if (load.AgentOwner != session.OwnControllerId || !animatedMangonelLoads.Contains(load.RequestId) ||
                !IsCurrentLoad(load, out var info, out var machine) ||
                !ReferenceEquals(info.Agent.CurrentlyUsedGameObject, machine.LoadAmmoStandingPoint) ||
                !HoldsLoadMissile(info, machine, load.GrantId)) continue;
            var action = info.Agent.GetCurrentAction(1);
            if (action == machine._loadAmmoBeginAnimationActionIndex)
                begunMangonelLoadActions.Add(load.RequestId);
            if (begunMangonelLoadActions.Contains(load.RequestId) &&
                action == machine._loadAmmoEndAnimationActionIndex && readyMangonelLoads.Add(load.RequestId))
                network.SendAll(load.WithPhase(MangonelLoadPhase.Ready, session.OwnControllerId));
        }
    }

    private void TickMangonelLoads()
    {
        foreach (var load in new List<NetworkMangonelLoad>(pendingMangonelLoads.Values))
        {
            pendingMangonelLoads.Remove(load.RequestId);
            ApplyMangonelLoad(load);
        }
        foreach (var load in new List<NetworkMangonelLoad>(mangonelLoads.Values))
        {
            if (!IsCurrentLoad(load, out var info, out var machine))
            {
                FinishMangonelLoad(load, stopOwnedUser: true);
                continue;
            }
            if (session.OwnControllerId == load.AgentOwner &&
                (!ReferenceEquals(info.Agent.CurrentlyUsedGameObject, machine.LoadAmmoStandingPoint) ||
                 !HoldsLoadMissile(info, machine, load.GrantId)))
            {
                network.SendAll(load.WithPhase(MangonelLoadPhase.Cancel, session.OwnControllerId));
                FinishMangonelLoad(load, stopOwnedUser: false);
                continue;
            }
        }

        foreach (var info in agentRegistry.GetAgents(session.OwnControllerId) ?? Array.Empty<CoopAgentInfo>())
        {
            if (info.Agent == null || !info.Agent.IsActive() || info.Agent.Mission != Mission.Current ||
                !(info.Agent.CurrentlyUsedGameObject is StandingPoint point) || info.SiegeEquipmentGrant == Guid.Empty ||
                mangonelLoads.ContainsKey(info.AgentId)) continue;
            foreach (var usable in machines)
            {
                if (!(usable is Mangonel machine) || !ReferenceEquals(machine.LoadAmmoStandingPoint, point) ||
                    !HoldsLoadMissile(info, machine, info.SiegeEquipmentGrant) ||
                    !TryGetMachineAuthority(machine.Id.Id, out var simulator, out int epoch, out int revision) ||
                    simulator == session.OwnControllerId) continue;
                var load = new NetworkMangonelLoad(session.InstanceId, Guid.NewGuid(), info.SiegeEquipmentGrant,
                    info.AgentId, info.CurrentAuthority, info.AuthorityRevision, machine.Id.Id, point.Id.Id,
                    simulator, epoch, revision, MangonelLoadPhase.Request, session.OwnControllerId);
                mangonelLoads.Add(info.AgentId, load);
                acceptedMangonelLoads[load.RequestId] = load;
                network.SendAll(load);
                break;
            }
        }

        foreach (var load in new List<NetworkMangonelLoad>(mangonelLoads.Values))
        {
            if (load.Simulator == session.OwnControllerId && IsCurrentLoad(load, out var info, out var machine))
                TryStartMangonelLoad(load, info, machine);
        }
    }

    private void HandleMangonelLoad(MessagePayload<NetworkMangonelLoad> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (mangonelLoadsDisposed || Mission.Current == null) return;
            RefreshMachineCache();
            ApplyMangonelLoad(payload.What);
        });
    }

    internal void ApplyMangonelLoad(NetworkMangonelLoad load)
    {
        if (mangonelLoadsDisposed || load == null || Mission.Current == null ||
            load.BattleId != session.InstanceId || string.IsNullOrEmpty(session.InstanceId)) return;
        if (TryApplyMangonelConsumption(load)) return;
        if (load.Phase == MangonelLoadPhase.OwnerConsumed)
        {
            if (load.RequestId != Guid.Empty && load.GrantId != Guid.Empty && load.SenderControllerId == load.AgentOwner &&
                (!agentRegistry.TryGetAgentInfo(load.AgentId, out var ownerInfo) ||
                 load.AgentRevision > ownerInfo.AuthorityRevision || !machinesById.ContainsKey(load.MachineId)))
                pendingMangonelLoads[load.RequestId] = load;
            return;
        }
        var authority = ClassifySnapshotAuthority(load.MachineId, load.HostEpoch,
            load.MachineRevision, load.Simulator);
        if (authority == SnapshotAuthority.Drop || load.HostEpoch < session.HostEpoch) return;
        if (authority == SnapshotAuthority.Buffer || !agentRegistry.TryGetAgentInfo(load.AgentId, out var registered) ||
            load.AgentRevision > registered.AuthorityRevision ||
            !machinesById.ContainsKey(load.MachineId))
        {
            if (!pendingMangonelLoads.TryGetValue(load.RequestId, out var pending) ||
                (pending.Phase != MangonelLoadPhase.Consumed && pending.Phase != MangonelLoadPhase.OwnerConsumed))
                pendingMangonelLoads[load.RequestId] = load;
            return;
        }
        if (!IsCurrentLoad(load, out var info, out var machine) ||
            (finishedMangonelLoads.Contains(load.RequestId) && load.Phase != MangonelLoadPhase.Consumed) ||
            consumedMangonelLoads.ContainsKey(load.RequestId)) return;
        switch (load.Phase)
        {
            case MangonelLoadPhase.Request:
            case MangonelLoadPhase.Ready:
                if (load.AgentOwner == session.OwnControllerId || !HoldsLoadMissile(info, machine, load.GrantId)) return;
                if (mangonelLoads.TryGetValue(load.AgentId, out var current) && !current.Matches(load))
                {
                    if (IsCurrentLoad(current, out _, out _)) return;
                    FinishMangonelLoad(current, stopOwnedUser: true);
                }
                mangonelLoads[load.AgentId] = load;
                acceptedMangonelLoads[load.RequestId] = load;
                if (load.Phase == MangonelLoadPhase.Ready) readyMangonelLoads.Add(load.RequestId);
                if (load.Simulator == session.OwnControllerId) TryStartMangonelLoad(load, info, machine);
                break;
            case MangonelLoadPhase.Animate:
                if (load.AgentOwner != session.OwnControllerId ||
                    !mangonelLoads.TryGetValue(load.AgentId, out var requested) || !requested.Matches(load) ||
                    !ReferenceEquals(info.Agent.CurrentlyUsedGameObject, machine.LoadAmmoStandingPoint) ||
                    !HoldsLoadMissile(info, machine, load.GrantId) || !animatedMangonelLoads.Add(load.RequestId)) return;
                // The loader's normal owner action stream remains the only writer on its puppets.
                if (!info.Agent.SetActionChannel(1, machine._loadAmmoBeginAnimationActionIndex, ignorePriority: false))
                {
                    network.SendAll(load.WithPhase(MangonelLoadPhase.Cancel, session.OwnControllerId));
                    FinishMangonelLoad(load, stopOwnedUser: true);
                }
                else if (info.Agent.GetCurrentAction(1) == machine._loadAmmoBeginAnimationActionIndex)
                    begunMangonelLoadActions.Add(load.RequestId);
                break;
            case MangonelLoadPhase.Cancel:
                FinishMangonelLoad(load, stopOwnedUser: load.SenderControllerId == load.Simulator);
                break;
        }
    }

    private bool TryApplyMangonelConsumption(NetworkMangonelLoad load)
    {
        if ((load.Phase != MangonelLoadPhase.Consumed && load.Phase != MangonelLoadPhase.OwnerConsumed) ||
            !TryResolveMangonelLoad(load, out var info, out var machine)) return false;
        bool current = IsCurrentLoad(load, out _, out _);
        if (load.Phase == MangonelLoadPhase.Consumed && !current &&
            (!acceptedMangonelLoads.TryGetValue(load.RequestId, out var accepted) || !accepted.Matches(load)))
            return false;
        if (consumedMangonelLoads.TryGetValue(load.RequestId, out var consumed) &&
            (load.Phase != MangonelLoadPhase.OwnerConsumed || consumed.Phase == MangonelLoadPhase.OwnerConsumed))
            return true;

        consumedMangonelLoads[load.RequestId] = load;
        if (info.ConsumeSiegeGrant(load.GrantId))
        {
            var weapon = info.Agent.Equipment[EquipmentIndex.ExtraWeaponSlot];
            using (new AllowedThread())
            {
                if (!weapon.IsEmpty && ReferenceEquals(weapon.Item, machine.OriginalMissileItem))
                    info.Agent.RemoveEquippedWeapon(EquipmentIndex.ExtraWeaponSlot);
            }
            info.RecordAuthoritativeEquipment(new AgentEquipmentData(info.Agent));
            messageBroker.Publish(this, new WeaponPickupApplied(info.AgentId,
                EquipmentIndex.ExtraWeaponSlot, Guid.Empty, 0, false, pickupId: load.GrantId));
        }
        // Historical completion settles equipment only; it cannot operate an old machine assignment.
        if (current) FinishMangonelLoad(load, stopOwnedUser: true);
        if (load.Phase == MangonelLoadPhase.Consumed && session.OwnControllerId == load.AgentOwner)
        {
            var terminal = load.WithPhase(MangonelLoadPhase.OwnerConsumed, session.OwnControllerId);
            consumedMangonelLoads[load.RequestId] = terminal;
            network.SendAll(terminal);
        }
        return true;
    }

    private void TryStartMangonelLoad(NetworkMangonelLoad load, CoopAgentInfo info, Mangonel machine)
    {
        if ((machine.LoadAmmoStandingPoint.HasUser && machine.LoadAmmoStandingPoint.UserAgent != info.Agent) ||
            (animatedMangonelLoads.Contains(load.RequestId) && machine.State != RangedSiegeWeapon.WeaponState.LoadingAmmo))
        {
            network.SendAll(load.WithPhase(MangonelLoadPhase.Cancel, session.OwnControllerId));
            FinishMangonelLoad(load, stopOwnedUser: true);
            return;
        }
        if (animatedMangonelLoads.Contains(load.RequestId) || !HoldsLoadMissile(info, machine, load.GrantId) ||
            machine.State != RangedSiegeWeapon.WeaponState.LoadingAmmo ||
            (machine.LoadAmmoStandingPoint.HasUser && machine.LoadAmmoStandingPoint.UserAgent != info.Agent) ||
            info.Agent.CurrentlyUsedGameObject != null) return;
        animatedMangonelLoads.Add(load.RequestId);
        network.Send(load.AgentOwner, load.WithPhase(MangonelLoadPhase.Animate, session.OwnControllerId));
    }

    private void HandleMangonelLoadTick(MessagePayload<MangonelLoadTick> payload)
    {
        var machine = payload.What.Machine;
        if (mangonelLoadsDisposed || machine.State != RangedSiegeWeapon.WeaponState.LoadingAmmo) return;
        foreach (var load in mangonelLoads.Values)
        {
            if (load.MachineId != machine.Id.Id || load.Simulator != session.OwnControllerId ||
                !animatedMangonelLoads.Contains(load.RequestId) || !readyMangonelLoads.Contains(load.RequestId) ||
                !IsCurrentLoad(load, out var info, out _) ||
                !HoldsLoadMissile(info, machine, load.GrantId) || machine.LoadAmmoStandingPoint.HasUser ||
                info.Agent.CurrentlyUsedGameObject != null) continue;
            if (info.Agent.GetCurrentAction(1) != machine._loadAmmoEndAnimationActionIndex) continue;
            // Attach only at the native consume branch; its earlier branch must not animate a foreign puppet.
            using (new AllowedThread())
            {
                info.Agent.UseGameObject(machine.LoadAmmoStandingPoint);
                if (info.Agent.GetCurrentAction(1) != machine._loadAmmoEndAnimationActionIndex)
                    info.Agent.StopUsingGameObject();
            }
            return;
        }
    }

    private void HandleMangonelAmmoConsumed(MessagePayload<MangonelAmmoConsumed> payload)
    {
        var consumed = payload.What;
        if (!agentRegistry.TryGetAgentInfo(consumed.Agent, out var info)) return;
        if (!mangonelLoads.TryGetValue(info.AgentId, out var load))
        {
            if (info.CurrentAuthority != session.OwnControllerId || info.SiegeEquipmentGrant == Guid.Empty ||
                !TryGetMachineAuthority(consumed.Machine.Id.Id, out var simulator, out int epoch, out int revision) ||
                simulator != session.OwnControllerId) return;
            load = new NetworkMangonelLoad(session.InstanceId, Guid.NewGuid(), info.SiegeEquipmentGrant,
                info.AgentId, info.CurrentAuthority, info.AuthorityRevision, consumed.Machine.Id.Id,
                consumed.Machine.LoadAmmoStandingPoint.Id.Id, simulator, epoch, revision,
                MangonelLoadPhase.Request, session.OwnControllerId);
            mangonelLoads.Add(info.AgentId, load);
            acceptedMangonelLoads[load.RequestId] = load;
            animatedMangonelLoads.Add(load.RequestId);
        }
        if (load.Simulator != session.OwnControllerId || !animatedMangonelLoads.Contains(load.RequestId) ||
            (load.AgentOwner != session.OwnControllerId && !readyMangonelLoads.Contains(load.RequestId)) ||
            !IsCurrentLoad(load, out _, out var machine) || machine != consumed.Machine ||
            info.SiegeEquipmentGrant != load.GrantId) return;
        var completion = load.WithPhase(MangonelLoadPhase.Consumed, session.OwnControllerId);
        network.SendAll(completion);
        ApplyMangonelLoad(completion);
    }

    private void FinishMangonelLoad(NetworkMangonelLoad load, bool stopOwnedUser)
    {
        finishedMangonelLoads.Add(load.RequestId);
        if (!mangonelLoads.TryGetValue(load.AgentId, out var current) || current.RequestId != load.RequestId) return;
        mangonelLoads.Remove(load.AgentId);
        bool started = animatedMangonelLoads.Remove(load.RequestId);
        begunMangonelLoadActions.Remove(load.RequestId);
        readyMangonelLoads.Remove(load.RequestId);
        if ((started || (stopOwnedUser && load.AgentOwner == session.OwnControllerId)) &&
            agentRegistry.TryGetAgentInfo(load.AgentId, out var info) && info.Agent != null &&
            info.CurrentAuthority == load.AgentOwner && info.AuthorityRevision == load.AgentRevision &&
            info.Agent.Mission == Mission.Current && info.Agent.CurrentlyUsedGameObject is StandingPoint point &&
            point.Id.Id == load.PointId && (stopOwnedUser || load.AgentOwner != session.OwnControllerId))
        {
            using (new AllowedThread()) info.Agent.StopUsingGameObject();
            if (load.AgentOwner == session.OwnControllerId)
                info.Agent.SetActionChannel(1, ActionIndexCache.act_none, ignorePriority: true);
        }
    }

    private void ClearMangonelLoads()
    {
        foreach (var load in new List<NetworkMangonelLoad>(mangonelLoads.Values))
            FinishMangonelLoad(load, stopOwnedUser: true);
        mangonelLoads.Clear();
        finishedMangonelLoads.Clear();
        animatedMangonelLoads.Clear();
        begunMangonelLoadActions.Clear();
        readyMangonelLoads.Clear();
        consumedMangonelLoads.Clear();
        acceptedMangonelLoads.Clear();
        pendingMangonelLoads.Clear();
    }

    private void ReplayMangonelLoads(string controllerId)
    {
        foreach (var load in mangonelLoads.Values)
        {
            if (load.AgentOwner == session.OwnControllerId && IsCurrentLoad(load, out _, out _))
            {
                network.Send(controllerId, load);
                if (readyMangonelLoads.Contains(load.RequestId))
                    network.Send(controllerId, load.WithPhase(MangonelLoadPhase.Ready, session.OwnControllerId));
            }
        }
        foreach (var load in consumedMangonelLoads.Values)
        {
            if ((load.Phase == MangonelLoadPhase.OwnerConsumed && load.AgentOwner == session.OwnControllerId &&
                TryResolveMangonelLoad(load, out _, out _)) ||
                (load.Simulator == session.OwnControllerId && IsCurrentLoad(load, out _, out _)))
                network.Send(controllerId, load);
        }
    }
}
