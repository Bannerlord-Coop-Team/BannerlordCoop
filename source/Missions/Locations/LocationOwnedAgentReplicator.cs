using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using Missions.Agents.Packets;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Locations;

/// <summary>
/// Host-side spawn replication for a settlement location mission (SR-011/SR-021): captures every
/// native non-player spawn (<see cref="AgentSpawnedInLocation"/> /
/// <see cref="MonsterSpawnedInLocation"/>, published by the location capture patches), registers the
/// agent under the local controller with a compact movement id, records its binding, and replicates
/// it over the mesh so peers spawn matching puppets driven by our movement. Also replays everything
/// we own to a joiner catching up (SR-025). Mirrors the battle <c>OwnedAgentReplicator</c>.
/// </summary>
public interface ILocationOwnedAgentReplicator : IDisposable
{
    /// <summary>Replay the settlement agents WE own to a peer that just joined (SR-025). Only the
    /// host owns NPCs, so this is a no-op on every other client.</summary>
    void ReplicateCurrentAgentsTo(string controllerId);

    /// <summary>[Host, game thread] Send newly captured agents as bounded batches once per mission tick.</summary>
    void FlushPendingSpawns();

    /// <summary>
    /// [Game thread] One of our replicated NPCs left the mission (native removal/fade or death,
    /// SR-026/SR-041): deregister it and queue the despawn broadcast for the next flush. No-op for
    /// agents we do not own or never replicated, so the mission-behavior removal hooks can forward
    /// every agent unconditionally.
    /// </summary>
    void NotifyAgentRemoved(Agent agent, LocationDespawnReason reason);
}

/// <inheritdoc cref="ILocationOwnedAgentReplicator"/>
public class LocationOwnedAgentReplicator : ILocationOwnedAgentReplicator
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationOwnedAgentReplicator>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopMissionComponent coopMissionComponent;
    private readonly ILocationSession session;
    private readonly ILocationAgentBindingMap bindingMap;
    private readonly ILocationPuppetRosterBinder rosterBinder;
    private readonly ILocationAgentSpawnBatchCodec spawnBatchCodec;
    private readonly List<LocationAgentSpawnData> pendingSpawns = new List<LocationAgentSpawnData>();
    private readonly List<(Guid agentId, LocationDespawnReason reason)> pendingDespawns
        = new List<(Guid, LocationDespawnReason)>();

    private readonly string movementScopeId;
    private ushort nextMovementId = 1;

    public LocationOwnedAgentReplicator(
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopMissionComponent coopMissionComponent,
        ILocationSession session,
        ILocationAgentBindingMap bindingMap,
        ILocationPuppetRosterBinder rosterBinder,
        ILocationAgentSpawnBatchCodec spawnBatchCodec)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.coopMissionComponent = coopMissionComponent;
        this.session = session;
        this.bindingMap = bindingMap;
        this.rosterBinder = rosterBinder;
        this.spawnBatchCodec = spawnBatchCodec;
        movementScopeId =
            session.OwnControllerId + ":" + Guid.NewGuid().ToString("N");

        messageBroker.Subscribe<AgentSpawnedInLocation>(Handle_AgentSpawnedInLocation);
        messageBroker.Subscribe<MonsterSpawnedInLocation>(Handle_MonsterSpawnedInLocation);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AgentSpawnedInLocation>(Handle_AgentSpawnedInLocation);
        messageBroker.Unsubscribe<MonsterSpawnedInLocation>(Handle_MonsterSpawnedInLocation);
        pendingSpawns.Clear();
    }

    // Reading agent transforms must run on the game thread; non-blocking so a network-thread caller
    // never deadlocks against a loading mission.
    public void ReplicateCurrentAgentsTo(string controllerId)
    {
        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            var records = BuildOwnedAgentRecords();
            if (records.Count == 0) return;

            IReadOnlyList<NetworkSpawnLocationAgents> batches =
                spawnBatchCodec.Encode(records, SpawnBatchPurpose.CatchUp);
            foreach (NetworkSpawnLocationAgents batch in batches)
                network.Send(controllerId, batch);

            LogBatchSend("Replayed", records.Count, batches, controllerId);
        }, context: nameof(ReplicateCurrentAgentsTo));
    }

    public void FlushPendingSpawns()
    {
        if (pendingSpawns.Count > 0)
        {
            IReadOnlyList<NetworkSpawnLocationAgents> batches =
                spawnBatchCodec.Encode(pendingSpawns, SpawnBatchPurpose.Initial);
            int recordCount = pendingSpawns.Count;
            pendingSpawns.Clear();

            foreach (NetworkSpawnLocationAgents batch in batches)
                network.SendAll(batch);

            LogBatchSend("Broadcast", recordCount, batches, null);
        }

        // Despawns flush AFTER spawns: a spawn+despawn pair captured in the same tick then applies in
        // capture order on the shared reliable-ordered stream.
        if (pendingDespawns.Count > 0)
        {
            var ids = new Guid[pendingDespawns.Count];
            var reasons = new byte[pendingDespawns.Count];
            for (int i = 0; i < pendingDespawns.Count; i++)
            {
                ids[i] = pendingDespawns[i].agentId;
                reasons[i] = (byte)pendingDespawns[i].reason;
            }
            pendingDespawns.Clear();

            network.SendAll(new NetworkDespawnLocationAgents(ids, reasons));
            Logger.Information("[LocationSync] Broadcast {Count} NPC despawn(s)", ids.Length);
        }
    }

    public void NotifyAgentRemoved(Agent agent, LocationDespawnReason reason)
    {
        if (agent == null) return;

        var registry = coopMissionComponent.AgentRegistry;
        if (!registry.TryGetAgentInfo(agent, out var info)) return;
        if (info.CurrentAuthority != session.OwnControllerId) return;   // not ours to despawn — its owner reports it
        if (!bindingMap.TryGet(info.AgentId, out _)) return;            // not a replicated NPC (e.g. a player agent)

        registry.RemoveAgent(info.AgentId);
        bindingMap.Forget(info.AgentId);
        pendingDespawns.Add((info.AgentId, reason));
        Logger.Debug("[LocationSync] Queued despawn of NPC {AgentId} ({Reason})", info.AgentId, reason);
    }

    // [Game thread] Build catch-up records for the settlement agents WE currently own, at their
    // CURRENT positions, from the live agent + its recorded binding.
    private List<LocationAgentSpawnData> BuildOwnedAgentRecords()
    {
        var records = new List<LocationAgentSpawnData>();
        foreach (var info in coopMissionComponent.AgentRegistry.GetAgents(session.OwnControllerId))
        {
            var agent = info.Agent;
            if (agent == null || !agent.IsActive()) continue;
            if (!bindingMap.TryGet(info.AgentId, out var binding)) continue; // not an NPC we replicate (e.g. our player agent)

            var record = BuildRecord(agent, info.AgentId, info.MovementId, info.MovementScopeId, binding,
                originalOwner: info.OriginalOwner);
            if (record != null)
                records.Add(record);
        }
        return records;
    }

    // [Host] A human NPC we spawned natively was captured. Register it under us and queue its record.
    private void Handle_AgentSpawnedInLocation(MessagePayload<AgentSpawnedInLocation> payload)
    {
        var agent = payload.What.Agent;
        if (agent == null || !(agent.Character is CharacterObject)) return;

        // The roster identity is extracted at capture time — the entry is resolvable via the agent's
        // origin right now, and any FUTURE host needs it to re-bind (SR-022).
        rosterBinder.TryExtractRosterData(agent, out var rosterData);
        var binding = new LocationAgentBinding(LocationAgentKind.Human, rosterData);

        CaptureOwnedAgent(agent, binding);
    }

    // [Host] An animal spawned via Mission.SpawnMonster was captured.
    private void Handle_MonsterSpawnedInLocation(MessagePayload<MonsterSpawnedInLocation> payload)
    {
        var agent = payload.What.Agent;
        if (agent == null) return;

        var binding = new LocationAgentBinding(
            LocationAgentKind.Animal,
            rosterEntry: null,
            itemId: payload.What.ItemId,
            harnessItemId: payload.What.HarnessItemId);

        CaptureOwnedAgent(agent, binding);
    }

    private void CaptureOwnedAgent(Agent agent, LocationAgentBinding binding)
    {
        string owner = session.OwnControllerId;
        var agentId = Guid.NewGuid();
        ushort movementId = AllocateMovementId();
        if (!coopMissionComponent.AgentRegistry.TryRegisterAgent(
                owner,
                owner,
                movementScopeId,
                agentId,
                movementId,
                agent))
            return;

        bindingMap.Record(agentId, binding);

        var record = BuildRecord(agent, agentId, movementId, movementScopeId, binding, originalOwner: owner);
        if (record == null) return;

        // Coalesce captures from native's spawn loop. The next mission tick emits bounded compressed
        // batches instead of dozens of singleton reliable messages.
        pendingSpawns.Add(record);
        Logger.Debug("[LocationSync] Captured own spawn (agent {AgentId}, {Kind}); queued for batched replication",
            agentId, binding.Kind);
    }

    private LocationAgentSpawnData BuildRecord(
        Agent agent,
        Guid agentId,
        ushort movementId,
        string scopeId,
        LocationAgentBinding binding,
        string originalOwner)
    {
        string characterId = null;
        if (binding.Kind == LocationAgentKind.Human)
        {
            if (!(agent.Character is CharacterObject character)) return null;
            if (!objectManager.TryGetId(character, out characterId))
            {
                Logger.Warning("[LocationSync] Skipping replication of agent {AgentId}: character '{Char}' has no object id",
                    agentId, character.StringId);
                return null;
            }
        }
        else if (string.IsNullOrEmpty(binding.ItemId))
        {
            return null; // an animal without an item identity cannot be re-spawned remotely
        }

        // Clothing colors are rolled at native spawn (villager palette uses raw MBRandom) — ship the
        // rolled values so puppets match exactly.
        uint color1 = 0, color2 = 0;
        try
        {
            color1 = agent.ClothingColor1;
            color2 = agent.ClothingColor2;
        }
        catch
        {
            // Unset clothing colors assert-throw on some agent kinds (animals); 0 means "engine default".
        }

        return new LocationAgentSpawnData(
            agentId,
            characterId,
            agent.Position,
            agent.GetMovementDirection(),
            agent.Health,
            session.OwnControllerId,
            binding.Kind == LocationAgentKind.Human ? agent.SpawnEquipment : null,
            binding.Kind == LocationAgentKind.Human ? agent.BodyPropertiesValue : default,
            color1,
            color2,
            binding.Kind,
            binding.ItemId,
            binding.HarnessItemId,
            movementId,
            scopeId,
            binding.Kind == LocationAgentKind.Human ? new AgentEquipmentData(agent) : (AgentEquipmentData?)null,
            binding.RosterEntry,
            originalOwnerControllerId: originalOwner);
    }

    private static void LogBatchSend(
        string action,
        int recordCount,
        IReadOnlyList<NetworkSpawnLocationAgents> batches,
        string controllerId)
    {
        int wireBytes = 0;
        int uncompressedBytes = 0;
        foreach (NetworkSpawnLocationAgents batch in batches)
        {
            wireBytes += batch.Payload?.Length ?? 0;
            uncompressedBytes += batch.UncompressedLength;
        }

        Logger.Information(
            "[LocationTraffic] {Action} spawn transfer {TransferId}: {RecordCount} record(s) in {BatchCount} batch(es), " +
            "{WireBytes} wire bytes from {UncompressedBytes} protobuf bytes{Controller}",
            action,
            batches.Count == 0 ? Guid.Empty : batches[0].TransferId,
            recordCount,
            batches.Count,
            wireBytes,
            uncompressedBytes,
            controllerId == null ? string.Empty : $" to {controllerId}");
    }

    private ushort AllocateMovementId()
    {
        ushort movementId = nextMovementId++;
        if (movementId == 0)
            throw new InvalidOperationException("A location mission exhausted its compact movement ids.");
        return movementId;
    }
}
