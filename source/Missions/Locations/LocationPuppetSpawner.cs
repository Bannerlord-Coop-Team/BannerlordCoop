using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Locations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Missions.Messages;
using SandBox;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Locations;

/// <summary>
/// Peer-side spawn application for a settlement location mission: spawns the NPCs the host replicates
/// over the mesh (<see cref="NetworkSpawnLocationAgents"/>) as local puppets driven by the host's
/// movement (SR-020..SR-025). Human puppets are built from a LOCAL roster entry's origin
/// (<see cref="ILocationPuppetRosterBinder"/>, SR-022) so native bookkeeping recognizes them; animals
/// re-spawn via <c>Mission.SpawnMonster</c> from their item identities. Records that arrive before
/// the mission or a render slot exists are buffered and drained on tick. Mirrors the battle
/// <c>PuppetSpawner</c>.
/// </summary>
public interface ILocationPuppetSpawner : IDisposable
{
    /// <summary>[Game thread] Retry buffered puppets (mission loading / render budget).</summary>
    void DrainPendingPuppets();
}

/// <inheritdoc cref="ILocationPuppetSpawner"/>
public class LocationPuppetSpawner : ILocationPuppetSpawner
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationPuppetSpawner>();
    private const int MaxBufferedSpawnsPerTick = 64;

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ILocationSession session;
    private readonly ILocationAgentBindingMap bindingMap;
    private readonly ILocationPuppetRosterBinder rosterBinder;
    private readonly IBattleAgentBudget agentBudget;
    private readonly ILocationAgentSpawnBatchCodec spawnBatchCodec;

    private readonly object pendingPuppetLock = new object();
    private readonly List<LocationAgentSpawnData> pendingPuppets = new List<LocationAgentSpawnData>();
    private readonly object withdrawnControllerLock = new object();
    private readonly HashSet<string> withdrawnControllers = new HashSet<string>();

    // Despawned ids (SR-026): a despawn can cross a still-buffered spawn record (or a catch-up
    // replay), so a tombstoned id must never (re)spawn. Per-mission lifetime bounds the set.
    private readonly object tombstoneLock = new object();
    private readonly HashSet<Guid> despawnedAgentIds = new HashSet<Guid>();

    public LocationPuppetSpawner(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        ILocationSession session,
        ILocationAgentBindingMap bindingMap,
        ILocationPuppetRosterBinder rosterBinder,
        IBattleAgentBudget agentBudget,
        ILocationAgentSpawnBatchCodec spawnBatchCodec)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.bindingMap = bindingMap;
        this.rosterBinder = rosterBinder;
        this.agentBudget = agentBudget;
        this.spawnBatchCodec = spawnBatchCodec;

        messageBroker.Subscribe<NetworkSpawnLocationAgents>(Handle_NetworkSpawnLocationAgents);
        messageBroker.Subscribe<NetworkDespawnLocationAgents>(Handle_NetworkDespawnLocationAgents);
        messageBroker.Subscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Subscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Subscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSpawnLocationAgents>(Handle_NetworkSpawnLocationAgents);
        messageBroker.Unsubscribe<NetworkDespawnLocationAgents>(Handle_NetworkDespawnLocationAgents);
        messageBroker.Unsubscribe<NetworkMissionPeerEntered>(Handle_PeerEntered);
        messageBroker.Unsubscribe<MissionPeerLeft>(Handle_PeerLeft);
        messageBroker.Unsubscribe<MissionPeerDisconnected>(Handle_PeerDisconnected);
    }

    // [Peer] The host's native systems removed NPCs (passage exit, churn, death): fade our puppets
    // out and tombstone the ids so a still-buffered spawn record cannot resurrect them (SR-026).
    private void Handle_NetworkDespawnLocationAgents(MessagePayload<NetworkDespawnLocationAgents> payload)
    {
        var ids = payload.What.AgentIds;
        if (ids == null || ids.Length == 0) return;

        lock (tombstoneLock)
        {
            foreach (var id in ids)
                despawnedAgentIds.Add(id);
        }
        lock (pendingPuppetLock)
        {
            pendingPuppets.RemoveAll(data => System.Array.IndexOf(ids, data.AgentId) >= 0);
        }

        GameThread.RunSafe(() =>
        {
            var registry = coopMissionComponent.AgentRegistry;
            int removed = 0;
            foreach (var id in ids)
            {
                if (!registry.TryGetAgentInfo(id, out var info)) continue;

                var agent = info.Agent;
                if (agent != null && agent.IsActive() && agent.Health > 0)
                {
                    bool hideMount = agent.HasMount && agent.MountAgent != null && agent.MountAgent.IsActive();
                    agent.FadeOut(false, hideMount);
                }

                coopMissionComponent.AgentMovementHandler.Interpolator.Forget(agent);
                registry.RemoveAgent(id);
                bindingMap.Forget(id);
                removed++;
            }

            if (removed > 0)
                Logger.Information("[LocationSync] Despawned {Count} NPC puppet(s) on the host's broadcast", removed);
        }, context: nameof(Handle_NetworkDespawnLocationAgents));
    }

    private void Handle_NetworkSpawnLocationAgents(MessagePayload<NetworkSpawnLocationAgents> payload)
    {
        NetworkSpawnLocationAgents message = payload.What;
        if (!spawnBatchCodec.TryDecode(message, out LocationAgentSpawnData[] agents))
        {
            Logger.Error(
                "[LocationTraffic] Rejected malformed spawn batch {TransferId} {BatchIndex}/{BatchCount} " +
                "with {RecordCount} declared record(s)",
                message.TransferId,
                message.BatchIndex + 1,
                message.BatchCount,
                message.RecordCount);
            return;
        }

        // One bounded game-thread action per wire batch preserves ReliableOrdered barriers without
        // adding one queue entry per agent. Non-blocking: this runs on the network thread while the
        // mission may still be loading.
        GameThread.RunSafe(
            () => SpawnPuppetBatch(message, agents),
            context: nameof(Handle_NetworkSpawnLocationAgents));
    }

    private void SpawnPuppetBatch(
        NetworkSpawnLocationAgents message,
        LocationAgentSpawnData[] agents)
    {
        if (Mission.Current == null)
        {
            // The mission can still be LOADING on a joiner when its catch-up burst lands — buffer, don't drop.
            lock (pendingPuppetLock)
            {
                foreach (LocationAgentSpawnData data in agents)
                    if (data != null && data.AgentId != Guid.Empty)
                        pendingPuppets.Add(data);
            }
            return;
        }

        int slotsAvailable = agentBudget.RemainingCapacity(agentBudget.CountLiveAgents(Mission.Current));
        foreach (LocationAgentSpawnData data in agents)
        {
            if (data == null || data.AgentId == Guid.Empty) continue;

            try
            {
                if (!TrySpawnPuppetNow(data, ref slotsAvailable))
                    lock (pendingPuppetLock) pendingPuppets.Add(data);
            }
            catch (Exception e)
            {
                Logger.Error(e, "[LocationSync] Failed to spawn puppet {AgentId}; dropping it", data.AgentId);
            }
        }
    }

    // [Game thread] Spawn one puppet, consuming render slots on success. Returns false to buffer.
    private bool TrySpawnPuppetNow(LocationAgentSpawnData data, ref int slotsAvailable)
    {
        var registry = coopMissionComponent.AgentRegistry;

        if (Mission.Current == null) return false;                      // still loading — buffer
        if (IsWithdrawn(data.OwnerControllerId)) return true;           // stale record after leave/drop — drop
        if (IsTombstoned(data.AgentId)) return true;                    // already despawned by the host — drop
        if (registry.TryGetAgentInfo(data.AgentId, out _)) return true; // already spawned (incl. our own natives) — dedupe

        int slotsNeeded = 1;
        if (data.Kind == LocationAgentKind.Human && data.SpawnEquipment != null)
            slotsNeeded = agentBudget.SlotsForEquipment(data.SpawnEquipment);
        if (slotsNeeded > slotsAvailable) return false;                 // at capacity — buffer

        Agent agent = data.Kind == LocationAgentKind.Animal
            ? SpawnAnimalPuppet(data)
            : SpawnHumanPuppet(data);
        if (agent == null) return true;                                 // unresolvable — drop (already logged)

        agent.FadeIn();
        if (data.Health > 0) agent.Health = data.Health;

        registry.TryRegisterAgent(
            data.OwnerControllerId,
            data.OriginalOwnerControllerId,
            data.MovementScopeId,
            data.AgentId,
            data.MovementId,
            agent);
        if (data.Kind == LocationAgentKind.Human && data.HasCurrentEquipment)
            data.CurrentEquipment.Apply(agent);

        bindingMap.Record(data.AgentId, new LocationAgentBinding(
            data.Kind, data.RosterEntry, data.ItemId, data.HarnessItemId));

        Logger.Debug("[LocationSync] Spawned {Kind} puppet (agent {AgentId})", data.Kind, data.AgentId);
        slotsAvailable -= slotsNeeded;
        return true;
    }

    private Agent SpawnHumanPuppet(LocationAgentSpawnData data)
    {
        if (!objectManager.TryGetObjectWithLogging(data.CharacterId, out CharacterObject character))
        {
            Logger.Warning("[LocationSync] Puppet skipped: unresolved character {Char} for agent {AgentId}",
                data.CharacterId, data.AgentId);
            return null;
        }

        // SR-022: bind the puppet to a LOCAL roster entry and build from ITS AgentData — the entry's
        // origin makes the puppet visible to native bookkeeping (IsAlreadySpawned, GetLocationCharacter,
        // passage guards) and is what a promoted host re-binds AI through.
        LocationCharacter entry = null;
        if (data.RosterEntry != null)
            rosterBinder.TryBindOrReconstruct(data.RosterEntry, out entry);

        AgentBuildData buildData;
        string actionSetCode;
        if (entry != null)
        {
            // NoHorses mirrors every native settlement spawn (the equipment override below already
            // carries no horse, since the host's agent spawned horseless — this is the belt).
            buildData = entry.GetAgentBuildData().NoHorses(noHorses: true);
            actionSetCode = entry.ActionSetCode;
        }
        else
        {
            // No roster identity (rare): an unbound stand-in — visuals still match, native roster
            // lookups just miss it, and adoption falls back to a stationary AI (SR-031).
            buildData = new AgentBuildData(character)
                .Monster(TaleWorlds.Core.FaceGen.GetMonsterWithSuffix(character.Race, "_settlement"))
                .TroopOrigin(new TaleWorlds.CampaignSystem.AgentOrigins.SimpleAgentOrigin(character))
                .NoHorses(noHorses: true);
            actionSetCode = ActionSetCode.GenerateActionSetNameWithSuffix(
                buildData.AgentMonster, character.IsFemale, "_villager");
        }

        buildData
            .Team(ResolvePuppetTeam(entry))
            .InitialPosition(data.Position)
            .InitialDirection(data.Direction)
            .BodyProperties(data.BodyProperties)
            .ClothingColor1(data.ClothingColor1)
            .ClothingColor2(data.ClothingColor2)
            .Controller(AgentControllerType.None);
        if (data.SpawnEquipment != null)
            buildData.Equipment(data.SpawnEquipment);

        Agent agent;
        LocationNpcGate.SuppressCapture = true;
        try
        {
            agent = Mission.Current.SpawnAgent(buildData);
        }
        finally
        {
            LocationNpcGate.SuppressCapture = false;
        }

        // Native applies the civilian action set explicitly after SpawnAgent — mirror it, or puppets
        // animate with the default (battle-flavored) set.
        if (!string.IsNullOrEmpty(actionSetCode))
        {
            var animationSystemData = buildData.AgentMonster.FillAnimationSystemData(
                MBGlobals.GetActionSet(actionSetCode), character.GetStepSize(), hasClippingPlane: false);
            agent.SetActionSet(ref animationSystemData);
        }

        return agent;
    }

    private Agent SpawnAnimalPuppet(LocationAgentSpawnData data)
    {
        if (!objectManager.TryGetObjectWithLogging(data.ItemId, out ItemObject item))
        {
            Logger.Warning("[LocationSync] Animal puppet skipped: unresolved item {Item} for agent {AgentId}",
                data.ItemId, data.AgentId);
            return null;
        }

        ItemObject harnessItem = null;
        if (!string.IsNullOrEmpty(data.HarnessItemId))
            objectManager.TryGetObject(data.HarnessItemId, out harnessItem);

        var equipmentElement = new EquipmentElement(item);
        var harnessElement = harnessItem != null ? new EquipmentElement(harnessItem) : default;
        var position = data.Position;
        var direction = data.Direction;

        LocationNpcGate.SuppressCapture = true;
        try
        {
            return Mission.Current.SpawnMonster(equipmentElement, harnessElement, in position, in direction);
        }
        finally
        {
            LocationNpcGate.SuppressCapture = false;
        }
    }

    // Native teams a settlement NPC by its roster entry's relation (MissionAgentHandler): Neutral →
    // no team, Friendly → player ally, Enemy → player enemy.
    private static Team ResolvePuppetTeam(LocationCharacter entry)
    {
        if (entry == null) return Team.Invalid;
        return entry.CharacterRelation switch
        {
            LocationCharacter.CharacterRelations.Friendly => Mission.Current.PlayerAllyTeam ?? Team.Invalid,
            LocationCharacter.CharacterRelations.Enemy => Mission.Current.PlayerEnemyTeam ?? Team.Invalid,
            _ => Team.Invalid,
        };
    }

    public void DrainPendingPuppets()
    {
        if (Mission.Current == null) return;

        LocationAgentSpawnData[] pending;
        lock (pendingPuppetLock)
        {
            if (pendingPuppets.Count == 0) return;
            int count = Math.Min(MaxBufferedSpawnsPerTick, pendingPuppets.Count);
            pending = new LocationAgentSpawnData[count];
            pendingPuppets.CopyTo(0, pending, 0, count);
            pendingPuppets.RemoveRange(0, count);
        }

        // Count the live remaining capacity ONCE for the whole drain and decrement as puppets spawn.
        int slotsAvailable = agentBudget.RemainingCapacity(agentBudget.CountLiveAgents(Mission.Current));

        foreach (var data in pending)
        {
            // One bad record must not abort the whole drain: drop on failure, never re-buffer it.
            try
            {
                if (!TrySpawnPuppetNow(data, ref slotsAvailable))
                    lock (pendingPuppetLock) pendingPuppets.Add(data);
            }
            catch (Exception e)
            {
                Logger.Error(e, "[LocationSync] Failed to spawn buffered puppet {AgentId}; dropping it", data.AgentId);
            }
        }
    }

    private void Handle_PeerEntered(MessagePayload<NetworkMissionPeerEntered> payload)
    {
        if (payload.What.InstanceId != null && payload.What.InstanceId != session.InstanceId) return;
        lock (withdrawnControllerLock)
        {
            withdrawnControllers.Remove(payload.What.ControllerId);
        }
    }

    private void Handle_PeerLeft(MessagePayload<MissionPeerLeft> payload)
    {
        MarkControllerWithdrawn(payload.What.ControllerId, payload.What.InstanceId);
    }

    private void Handle_PeerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
    {
        MarkControllerWithdrawn(payload.What.ControllerId, payload.What.InstanceId);
    }

    // A departed controller's not-yet-applied records are stale: the NPCs that already spawned are
    // what a promoted host adopts (SR-014); a record applied AFTER the departure would create a
    // puppet no adoption sweep has seen.
    private void MarkControllerWithdrawn(string controllerId, string instanceId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        if (instanceId != null && instanceId != session.InstanceId) return;

        lock (withdrawnControllerLock)
        {
            withdrawnControllers.Add(controllerId);
        }

        lock (pendingPuppetLock)
        {
            pendingPuppets.RemoveAll(data => data.OwnerControllerId == controllerId);
        }
    }

    private bool IsWithdrawn(string controllerId)
    {
        lock (withdrawnControllerLock)
        {
            return withdrawnControllers.Contains(controllerId);
        }
    }

    private bool IsTombstoned(Guid agentId)
    {
        lock (tombstoneLock)
        {
            return despawnedAgentIds.Contains(agentId);
        }
    }
}
