using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>Synchronizes authoritative agent weapon drops and their runtime world-item identities.</summary>
public interface IWeaponDropHandler : IHandler
{
    void CatchUpJoiner(string controllerId);
}

/// <inheritdoc cref="IWeaponDropHandler"/>
public class WeaponDropHandler : IWeaponDropHandler
{
    private const int MaxPendingDropsPerSlot = 8;
    private const int MaxObjectIdLength = 256;
    private const int MaxBannerCodeLength = 4096;
    private const int KnownSpawnFlagsMask = 0x7F;
    private const float MaxDropLifeTimeSeconds = 180f;
    private static readonly TimeSpan ObservedDropTimeout = TimeSpan.FromSeconds(5);

    private static readonly ILogger Logger = LogManager.GetLogger<WeaponDropHandler>();

    private sealed class ObservedDrop
    {
        public Agent Agent { get; }
        public EquipmentIndex EquipmentIndex { get; }
        public MissionWeapon Weapon { get; }
        public SpawnedItemEntity Item { get; }
        public DateTime ExpiresAtUtc { get; }
        public bool HasLaterAuthoritativeSlotTransition { get; set; }
        public bool HasPendingPickup { get; private set; }
        public bool PendingPickupConsumed { get; private set; }
        public short PendingRemainingAmount { get; private set; }

        public ObservedDrop(
            Agent agent,
            EquipmentIndex equipmentIndex,
            MissionWeapon weapon,
            SpawnedItemEntity item)
        {
            Agent = agent;
            EquipmentIndex = equipmentIndex;
            Weapon = weapon;
            Item = item;
            ExpiresAtUtc = DateTime.UtcNow.Add(ObservedDropTimeout);
        }

        public void RecordPendingPickup(WeaponPickedup pickup)
        {
            HasPendingPickup = true;
            PendingPickupConsumed = pickup.WorldItemConsumed;
            PendingRemainingAmount = pickup.ResultingWorldItemAmount;
            HasLaterAuthoritativeSlotTransition = true;
        }
    }

    private readonly INetworkAgentRegistry networkAgentRegistry;
    private readonly INetworkWorldItemRegistry worldItemRegistry;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleNetwork network;
    private readonly IObjectManager objectManager;
    private readonly IWeaponDropWorldItemSpawner worldItemSpawner;
    private readonly Dictionary<(Guid AgentId, EquipmentIndex Slot), Queue<ObservedDrop>> pendingDrops =
        new Dictionary<(Guid AgentId, EquipmentIndex Slot), Queue<ObservedDrop>>();
    private readonly Dictionary<Guid, NetworkWeaponDropped> activeDrops =
        new Dictionary<Guid, NetworkWeaponDropped>();
    private readonly HashSet<Guid> appliedDropIds = new HashSet<Guid>();
    private readonly HashSet<Guid> retiredWorldItemIds = new HashSet<Guid>();
    private readonly Dictionary<Guid, short> remainingWorldItemAmounts = new Dictionary<Guid, short>();
    private bool disposed;

    public WeaponDropHandler(
        INetworkAgentRegistry networkAgentRegistry,
        INetworkWorldItemRegistry worldItemRegistry,
        IMessageBroker messageBroker,
        IBattleNetwork network,
        IObjectManager objectManager,
        IWeaponDropWorldItemSpawner worldItemSpawner)
    {
        this.networkAgentRegistry = networkAgentRegistry;
        this.worldItemRegistry = worldItemRegistry;
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.worldItemSpawner = worldItemSpawner;

        messageBroker.Subscribe<WeaponDropped>(HandleWeaponDropped);
        messageBroker.Subscribe<NetworkWeaponDropped>(HandleNetworkWeaponDropped);
        messageBroker.Subscribe<WeaponPickedup>(HandleWeaponPickedup);
        messageBroker.Subscribe<WeaponPickupApplied>(HandleWeaponPickupApplied);
    }

    ~WeaponDropHandler()
    {
        Dispose();
    }

    public void Dispose()
    {
        disposed = true;
        messageBroker.Unsubscribe<WeaponDropped>(HandleWeaponDropped);
        messageBroker.Unsubscribe<NetworkWeaponDropped>(HandleNetworkWeaponDropped);
        messageBroker.Unsubscribe<WeaponPickedup>(HandleWeaponPickedup);
        messageBroker.Unsubscribe<WeaponPickupApplied>(HandleWeaponPickupApplied);
        pendingDrops.Clear();
        activeDrops.Clear();
        appliedDropIds.Clear();
        retiredWorldItemIds.Clear();
        remainingWorldItemAmounts.Clear();
        GC.SuppressFinalize(this);
    }

    public void CatchUpJoiner(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        GameThread.RunSafe(
            () => SendCatchUp(controllerId),
            context: nameof(CatchUpJoiner));
    }

    private void HandleWeaponDropped(MessagePayload<WeaponDropped> payload)
    {
        WeaponDropped dropped = payload.What;
        if (!networkAgentRegistry.TryGetAgentInfo(dropped.Agent, out CoopAgentInfo agentInfo))
        {
            Logger.Debug("[WeaponDrop] Ignored unregistered agent drop slot={EquipmentIndex}", dropped.EquipmentIndex);
            return;
        }

        if (!networkAgentRegistry.IsLocallyControlled(dropped.Agent))
        {
            TrackObservedDrop(agentInfo.AgentId, dropped);
            return;
        }

        if (!TryCreateTransition(agentInfo, dropped, out NetworkWeaponDropped message)) return;

        appliedDropIds.Add(message.DropId);
        if (message.WorldItemId != Guid.Empty && dropped.DroppedItem != null)
            activeDrops[message.WorldItemId] = message;

        network.SendAll(message);
        Logger.Debug(
            "[WeaponDrop] Sent drop={DropId} origin={OriginControllerId} agent={AgentId} slot={EquipmentIndex} " +
            "item={ItemObjectId} worldItem={WorldItemId}",
            message.DropId,
            message.OriginControllerId,
            message.AgentId,
            message.EquipmentIndex,
            message.ItemObjectId,
            message.WorldItemId);
    }

    private void HandleNetworkWeaponDropped(MessagePayload<NetworkWeaponDropped> payload)
    {
        NetworkWeaponDropped message = payload.What;
        GameThread.RunSafe(
            () => ApplyNetworkDrop(message),
            context: nameof(HandleNetworkWeaponDropped));
    }

    private void HandleWeaponPickedup(MessagePayload<WeaponPickedup> payload)
    {
        WeaponPickedup pickedup = payload.What;
        if (!networkAgentRegistry.IsLocallyControlled(pickedup.Agent) ||
            pickedup.WorldItem == null)
        {
            return;
        }

        if (!worldItemRegistry.TryGetId(pickedup.WorldItem, out Guid worldItemId))
        {
            RecordPendingObservedPickup(pickedup);
            return;
        }

        if (pickedup.WorldItemConsumed)
        {
            GameThread.EnqueueSafe(
                () => RetireWorldItem(worldItemId),
                context: nameof(HandleWeaponPickedup));
        }
        else
        {
            remainingWorldItemAmounts[worldItemId] = pickedup.ResultingWorldItemAmount;
        }
    }

    private void HandleWeaponPickupApplied(MessagePayload<WeaponPickupApplied> payload)
    {
        WeaponPickupApplied applied = payload.What;
        if (applied.WorldItemId != Guid.Empty)
        {
            if (applied.WorldItemConsumed)
                RetireWorldItem(applied.WorldItemId);
            else
                remainingWorldItemAmounts[applied.WorldItemId] = applied.ResultingWorldItemAmount;
        }

        var key = (applied.AgentId, applied.EquipmentIndex);
        if (!IsValidEquipmentIndex(applied.EquipmentIndex) ||
            !pendingDrops.TryGetValue(key, out Queue<ObservedDrop> queue))
        {
            return;
        }

        foreach (ObservedDrop observed in queue)
            observed.HasLaterAuthoritativeSlotTransition = true;
    }

    private bool TryCreateTransition(
        CoopAgentInfo agentInfo,
        WeaponDropped dropped,
        out NetworkWeaponDropped message)
    {
        message = null;
        if (!IsValidEquipmentIndex(dropped.EquipmentIndex) || dropped.DroppedWeapon.IsEmpty)
        {
            Logger.Warning(
                "[WeaponDrop] Cannot serialize drop for agent={AgentId} slot={EquipmentIndex}: empty or invalid weapon",
                agentInfo.AgentId,
                dropped.EquipmentIndex);
            return false;
        }

        if (!TrySerializeWeapon(dropped.DroppedWeapon, out string itemId, out string modifierId))
            return false;

        Guid worldItemId = Guid.Empty;
        MatrixFrame frame = default;
        int spawnFlags = 0;
        bool hasLifeTime = false;
        float remainingLifeTime = 0f;
        if (dropped.DroppedItem != null)
        {
            if (!worldItemSpawner.TryGetState(
                    dropped.DroppedItem,
                    out frame,
                    out remainingLifeTime))
            {
                Logger.Error(
                    "[WeaponDrop] Cannot serialize inaccessible world item for agent={AgentId} slot={EquipmentIndex}",
                    agentInfo.AgentId,
                    dropped.EquipmentIndex);
                return false;
            }

            worldItemId = worldItemRegistry.GetOrCreateId(dropped.DroppedItem);
            spawnFlags = (int)dropped.DroppedItem.SpawnFlags;
            hasLifeTime = dropped.DroppedItem.HasLifeTime;
        }

        Guid dropId = worldItemId == Guid.Empty ? Guid.NewGuid() : worldItemId;
        message = new NetworkWeaponDropped(
            dropId,
            agentInfo.AgentId,
            dropped.EquipmentIndex,
            worldItemId,
            agentInfo.CurrentAuthority,
            itemId,
            modifierId,
            dropped.DroppedWeapon.Banner?.Serialize(),
            dropped.DroppedWeapon.RawDataForNetwork,
            frame.origin,
            frame.rotation,
            spawnFlags,
            hasLifeTime,
            remainingLifeTime,
            new AgentEquipmentData(dropped.Agent),
            isCatchUp: false);
        return true;
    }

    private bool TryCreateCatchUp(
        NetworkWeaponDropped source,
        SpawnedItemEntity item,
        out NetworkWeaponDropped message)
    {
        message = null;
        if (!worldItemSpawner.TryGetState(
                item,
                out MatrixFrame frame,
                out float remainingLifeTime)) return false;

        MissionWeapon weapon = item.WeaponCopy;
        if (weapon.IsEmpty || !TrySerializeWeapon(weapon, out string itemId, out string modifierId))
            return false;

        message = new NetworkWeaponDropped(
            source.DropId,
            source.AgentId,
            source.EquipmentIndex,
            source.WorldItemId,
            source.OriginControllerId,
            itemId,
            modifierId,
            weapon.Banner?.Serialize(),
            weapon.RawDataForNetwork,
            frame.origin,
            frame.rotation,
            (int)item.SpawnFlags,
            item.HasLifeTime,
            remainingLifeTime,
            currentEquipment: null,
            isCatchUp: true);
        return true;
    }

    private bool TrySerializeWeapon(
        MissionWeapon weapon,
        out string itemId,
        out string modifierId)
    {
        itemId = null;
        modifierId = null;
        if (weapon.Item == null || !objectManager.TryGetIdWithLogging(weapon.Item, out itemId))
            return false;
        if (weapon.ItemModifier != null &&
            !objectManager.TryGetIdWithLogging(weapon.ItemModifier, out modifierId))
            return false;
        return true;
    }

    private void TrackObservedDrop(Guid agentId, WeaponDropped dropped)
    {
        var key = (agentId, dropped.EquipmentIndex);
        if (!pendingDrops.TryGetValue(key, out Queue<ObservedDrop> queue))
        {
            queue = new Queue<ObservedDrop>();
            pendingDrops.Add(key, queue);
        }

        while (queue.Count >= MaxPendingDropsPerSlot)
        {
            ObservedDrop discarded = null;
            var retained = new Queue<ObservedDrop>();
            while (queue.Count > 0)
            {
                ObservedDrop candidate = queue.Dequeue();
                if (discarded == null && !candidate.HasPendingPickup)
                    discarded = candidate;
                else
                    retained.Enqueue(candidate);
            }
            queue = retained;
            pendingDrops[key] = queue;
            if (discarded == null) break;
            AbandonObservedWorldItemIdentity(discarded);
            DiscardObservedDrop(discarded, agentId, dropped.EquipmentIndex, "pending-limit");
        }

        var observed = new ObservedDrop(
            dropped.Agent,
            dropped.EquipmentIndex,
            dropped.DroppedWeapon,
            dropped.DroppedItem);
        queue.Enqueue(observed);
        if (observed.Item != null)
            messageBroker.Publish(this, new WorldItemIdentityPending(observed.Item));
        ScheduleObservedDropExpiry(key, observed);
        Logger.Debug(
            "[WeaponDrop] Observed local peer drop agent={AgentId} slot={EquipmentIndex} pending={PendingCount}",
            agentId,
            dropped.EquipmentIndex,
            queue.Count);
    }

    private void ApplyNetworkDrop(NetworkWeaponDropped message)
    {
        if (!TryBuildCanonicalWeapon(message, out MissionWeapon canonical)) return;

        if (message.WorldItemId != Guid.Empty &&
            remainingWorldItemAmounts.TryGetValue(message.WorldItemId, out short remainingAmount))
        {
            canonical.Amount = remainingAmount;
        }

        if (message.IsCatchUp)
        {
            ApplyCatchUp(message, ref canonical);
            return;
        }

        if (appliedDropIds.Contains(message.DropId))
        {
            Logger.Debug("[WeaponDrop] Ignored duplicate drop={DropId}", message.DropId);
            return;
        }

        if (message.WorldItemId != Guid.Empty && retiredWorldItemIds.Contains(message.WorldItemId))
        {
            appliedDropIds.Add(message.DropId);
            Logger.Debug(
                "[WeaponDrop] Ignored retired drop={DropId} worldItem={WorldItemId}",
                message.DropId,
                message.WorldItemId);
            return;
        }

        if (!networkAgentRegistry.TryGetAgentInfo(message.AgentId, out CoopAgentInfo agentInfo))
        {
            Logger.Warning("[WeaponDrop] No agent found for drop={DropId} agent={AgentId}", message.DropId, message.AgentId);
            return;
        }

        bool hadObservedDrop = TryTakeObservedDrop(
            message.AgentId,
            message.EquipmentIndex,
            canonical,
            message.WorldItemId != Guid.Empty,
            out ObservedDrop observedDrop);
        if (hadObservedDrop)
        {
            if (observedDrop.HasPendingPickup)
            {
                if (observedDrop.PendingPickupConsumed)
                {
                    ApplyCurrentEquipment(message, agentInfo);
                    appliedDropIds.Add(message.DropId);
                    RetireWorldItem(message.WorldItemId);
                    ResolveObservedWorldItemIdentity(observedDrop, message.WorldItemId);
                    return;
                }

                canonical.Amount = observedDrop.PendingRemainingAmount;
            }

            if (!ReconcileObservedDrop(message, ref canonical, observedDrop, out SpawnedItemEntity observedItem))
                return;

            ApplyCurrentEquipment(message, agentInfo);
            RecordApplied(message, observedItem);
            if (observedDrop.HasPendingPickup)
                remainingWorldItemAmounts[message.WorldItemId] = observedDrop.PendingRemainingAmount;
            ResolveObservedWorldItemIdentity(observedDrop, message.WorldItemId);
            Logger.Debug(
                "[WeaponDrop] Applied observed drop={DropId} agent={AgentId} slot={EquipmentIndex} worldItem={WorldItemId}",
                message.DropId,
                message.AgentId,
                message.EquipmentIndex,
                message.WorldItemId);
            return;
        }

        TryGetRegisteredCanonical(
            message,
            canonical,
            out SpawnedItemEntity registeredItem,
            out bool blocked);
        if (blocked) return;

        if (!ApplyAuthoritativeTransition(
                message,
                agentInfo,
                ref canonical,
                registeredItem,
                out SpawnedItemEntity droppedItem))
            return;

        RecordApplied(message, droppedItem);
        Logger.Debug(
            "[WeaponDrop] Applied authoritative drop={DropId} origin={OriginControllerId} agent={AgentId} " +
            "slot={EquipmentIndex} item={ItemObjectId} worldItem={WorldItemId}",
            message.DropId,
            message.OriginControllerId,
            message.AgentId,
            message.EquipmentIndex,
            message.ItemObjectId,
            message.WorldItemId);
    }

    private void ApplyCatchUp(NetworkWeaponDropped message, ref MissionWeapon canonical)
    {
        if (message.WorldItemId == Guid.Empty || retiredWorldItemIds.Contains(message.WorldItemId))
            return;

        if (TryGetRegisteredCanonical(message, canonical, out SpawnedItemEntity registeredItem, out bool blocked))
        {
            RecordApplied(message, registeredItem);
            return;
        }
        if (blocked) return;

        if (!TrySpawnCanonical(message, ref canonical, out SpawnedItemEntity spawnedItem)) return;

        RecordApplied(message, spawnedItem);
        Logger.Debug(
            "[WeaponDrop] Applied catch-up drop={DropId} worldItem={WorldItemId} origin={OriginControllerId}",
            message.DropId,
            message.WorldItemId,
            message.OriginControllerId);
    }

    private bool ReconcileObservedDrop(
        NetworkWeaponDropped message,
        ref MissionWeapon canonical,
        ObservedDrop observed,
        out SpawnedItemEntity worldItem)
    {
        worldItem = null;
        bool observedMatches = observed != null &&
            (observed.HasPendingPickup || WeaponMatches(observed.Weapon, canonical)) &&
            observed.Item != null &&
            worldItemSpawner.IsPresent(observed.Item) &&
            WeaponMatches(observed.Item.WeaponCopy, canonical);

        if (message.WorldItemId == Guid.Empty)
        {
            if (observed?.Item != null && worldItemSpawner.IsPresent(observed.Item))
                return RemoveObservedItem(observed.Item, message, "unexpected-world-item");
            return true;
        }

        if (observedMatches)
        {
            worldItem = observed.Item;
            worldItemRegistry.Register(message.WorldItemId, worldItem);
            return true;
        }

        if (observed?.Item != null &&
            worldItemSpawner.IsPresent(observed.Item) &&
            !RemoveObservedItem(observed.Item, message, "weapon-mismatch"))
        {
            return false;
        }

        Logger.Warning(
            "[WeaponDrop] Repaired observed mismatch drop={DropId} agent={AgentId} slot={EquipmentIndex} " +
            "item={ItemObjectId} worldItem={WorldItemId}",
            message.DropId,
            message.AgentId,
            message.EquipmentIndex,
            message.ItemObjectId,
            message.WorldItemId);
        return TrySpawnCanonical(message, ref canonical, out worldItem);
    }

    private bool ApplyAuthoritativeTransition(
        NetworkWeaponDropped message,
        CoopAgentInfo agentInfo,
        ref MissionWeapon canonical,
        SpawnedItemEntity registeredItem,
        out SpawnedItemEntity worldItem)
    {
        worldItem = registeredItem;
        Agent agent = agentInfo.Agent;
        if (agent == null || agent.Equipment == null) return false;

        MissionWeapon current = agent.Equipment[message.EquipmentIndex];
        if (registeredItem != null)
        {
            if (!current.IsEmpty)
            {
                using (new AllowedThread())
                    agent.RemoveEquippedWeapon(message.EquipmentIndex);
                Logger.Warning(
                    "[WeaponDrop] Reused registered item and cleared slot drop={DropId} agent={AgentId} " +
                    "slot={EquipmentIndex}",
                    message.DropId,
                    message.AgentId,
                    message.EquipmentIndex);
            }
        }
        else if (WeaponMatches(current, canonical))
        {
            HashSet<SpawnedItemEntity> existingItems = WeaponDropItemTracker.Capture();
            using (new AllowedThread())
                agent.DropItem(message.EquipmentIndex);
            worldItem = WeaponDropItemTracker.FindDroppedItem(existingItems);

            if (message.WorldItemId == Guid.Empty)
            {
                if (worldItem != null && worldItemSpawner.IsPresent(worldItem) &&
                    !RemoveObservedItem(worldItem, message, "unexpected-native-world-item"))
                {
                    return false;
                }
                worldItem = null;
            }
            else if (worldItem == null ||
                     !worldItemSpawner.IsPresent(worldItem) ||
                     !WeaponMatches(worldItem.WeaponCopy, canonical))
            {
                if (worldItem != null && worldItemSpawner.IsPresent(worldItem) &&
                    !RemoveObservedItem(worldItem, message, "native-weapon-mismatch"))
                {
                    return false;
                }
                if (!TrySpawnCanonical(message, ref canonical, out worldItem)) return false;
            }
            else
            {
                worldItemRegistry.Register(message.WorldItemId, worldItem);
            }
        }
        else
        {
            if (!current.IsEmpty)
            {
                using (new AllowedThread())
                    agent.RemoveEquippedWeapon(message.EquipmentIndex);
                Logger.Warning(
                    "[WeaponDrop] Cleared mismatched slot for drop={DropId} agent={AgentId} " +
                    "slot={EquipmentIndex} expected={ItemObjectId}",
                    message.DropId,
                    message.AgentId,
                    message.EquipmentIndex,
                    message.ItemObjectId);
            }

            if (message.WorldItemId != Guid.Empty &&
                !TrySpawnCanonical(message, ref canonical, out worldItem))
            {
                return false;
            }
        }

        ApplyCurrentEquipment(message, agentInfo);
        return true;
    }

    private static void ApplyCurrentEquipment(NetworkWeaponDropped message, CoopAgentInfo agentInfo)
    {
        if (!message.HasCurrentEquipment) return;

        agentInfo.RecordAuthoritativeEquipment(message.CurrentEquipment);
        using (new AllowedThread())
            message.CurrentEquipment.Apply(agentInfo.Agent);
    }

    private bool TryGetRegisteredCanonical(
        NetworkWeaponDropped message,
        MissionWeapon canonical,
        out SpawnedItemEntity item,
        out bool blocked)
    {
        item = null;
        blocked = false;
        if (message.WorldItemId == Guid.Empty ||
            !worldItemRegistry.TryGet(message.WorldItemId, out SpawnedItemEntity registered))
        {
            return false;
        }

        if (worldItemSpawner.IsPresent(registered) && WeaponMatches(registered.WeaponCopy, canonical))
        {
            item = registered;
            return true;
        }

        worldItemRegistry.Remove(message.WorldItemId);
        if (worldItemSpawner.IsPresent(registered) && !worldItemSpawner.TryRemove(registered))
        {
            Logger.Error(
                "[WeaponDrop] Failed to remove mismatched registered item drop={DropId} worldItem={WorldItemId}",
                message.DropId,
                message.WorldItemId);
            blocked = true;
            return false;
        }

        Logger.Warning(
            "[WeaponDrop] Replacing mismatched registered item drop={DropId} worldItem={WorldItemId}",
            message.DropId,
            message.WorldItemId);
        return false;
    }

    private bool TrySpawnCanonical(
        NetworkWeaponDropped message,
        ref MissionWeapon canonical,
        out SpawnedItemEntity item)
    {
        item = null;
        if (message.WorldItemId == Guid.Empty) return true;

        var frame = new MatrixFrame(message.Rotation, message.Position);
        if (!worldItemSpawner.TrySpawn(
                ref canonical,
                (Mission.WeaponSpawnFlags)message.SpawnFlags,
                message.HasLifeTime,
                message.RemainingLifeTime,
                frame,
                out item))
        {
            Logger.Error(
                "[WeaponDrop] Failed to spawn canonical item drop={DropId} agent={AgentId} " +
                "item={ItemObjectId} worldItem={WorldItemId}",
                message.DropId,
                message.AgentId,
                message.ItemObjectId,
                message.WorldItemId);
            return false;
        }

        worldItemRegistry.Register(message.WorldItemId, item);
        return true;
    }

    private bool RemoveObservedItem(
        SpawnedItemEntity item,
        NetworkWeaponDropped message,
        string reason)
    {
        if (worldItemRegistry.TryGetId(item, out Guid existingId))
            worldItemRegistry.Remove(existingId);
        if (worldItemSpawner.TryRemove(item)) return true;

        Logger.Error(
            "[WeaponDrop] Failed to remove local item drop={DropId} agent={AgentId} " +
            "slot={EquipmentIndex} reason={Reason}",
            message.DropId,
            message.AgentId,
            message.EquipmentIndex,
            reason);
        return false;
    }

    private bool TryTakeObservedDrop(
        Guid agentId,
        EquipmentIndex equipmentIndex,
        MissionWeapon canonical,
        bool expectsWorldItem,
        out ObservedDrop observed)
    {
        var key = (agentId, equipmentIndex);
        observed = null;
        if (!pendingDrops.TryGetValue(key, out Queue<ObservedDrop> queue) || queue.Count == 0)
            return false;

        var retained = new Queue<ObservedDrop>();
        while (queue.Count > 0)
        {
            ObservedDrop candidate = queue.Dequeue();
            bool matches = WeaponMatches(candidate.Weapon, canonical) &&
                (!expectsWorldItem ||
                 candidate.HasPendingPickup ||
                 (candidate.Item != null &&
                  worldItemSpawner.IsPresent(candidate.Item) &&
                  WeaponMatches(candidate.Item.WeaponCopy, canonical)));
            if (observed == null && matches)
            {
                observed = candidate;
                continue;
            }

            if (observed == null && candidate.HasPendingPickup)
                retained.Enqueue(candidate);
            else if (observed == null)
            {
                AbandonObservedWorldItemIdentity(candidate);
                DiscardObservedDrop(candidate, agentId, equipmentIndex, "superseded-authoritative");
            }
            else
                retained.Enqueue(candidate);
        }

        if (retained.Count == 0)
            pendingDrops.Remove(key);
        else
            pendingDrops[key] = retained;
        return observed != null;
    }

    private void RecordPendingObservedPickup(WeaponPickedup pickup)
    {
        foreach (Queue<ObservedDrop> queue in pendingDrops.Values)
        {
            foreach (ObservedDrop observed in queue)
            {
                if (!ReferenceEquals(observed.Item, pickup.WorldItem)) continue;
                observed.RecordPendingPickup(pickup);
                return;
            }
        }
    }

    private void ResolveObservedWorldItemIdentity(ObservedDrop observed, Guid worldItemId)
    {
        if (observed?.Item == null || worldItemId == Guid.Empty) return;
        messageBroker.Publish(this, new WorldItemIdentityResolved(observed.Item, worldItemId));
    }

    private void AbandonObservedWorldItemIdentity(ObservedDrop observed)
    {
        if (observed?.Item != null)
            messageBroker.Publish(this, new WorldItemIdentityAbandoned(observed.Item));
    }

    private void DiscardObservedDrop(
        ObservedDrop observed,
        Guid agentId,
        EquipmentIndex equipmentIndex,
        string reason)
    {
        if (observed?.Item == null || !worldItemSpawner.IsPresent(observed.Item)) return;
        if (worldItemRegistry.TryGetId(observed.Item, out Guid existingId))
            worldItemRegistry.Remove(existingId);
        if (worldItemSpawner.TryRemove(observed.Item)) return;

        Logger.Error(
            "[WeaponDrop] Failed to discard observed item agent={AgentId} slot={EquipmentIndex} reason={Reason}",
            agentId,
            equipmentIndex,
            reason);
    }

    private bool TryBuildCanonicalWeapon(
        NetworkWeaponDropped message,
        out MissionWeapon weapon)
    {
        weapon = default;
        if (message == null ||
            message.DropId == Guid.Empty ||
            message.AgentId == Guid.Empty ||
            !IsValidEquipmentIndex(message.EquipmentIndex) ||
            string.IsNullOrEmpty(message.OriginControllerId) ||
            string.IsNullOrEmpty(message.ItemObjectId) ||
            message.ItemObjectId.Length > MaxObjectIdLength ||
            (message.ItemModifierId?.Length ?? 0) > MaxObjectIdLength ||
            (message.BannerCode?.Length ?? 0) > MaxBannerCodeLength ||
            (message.SpawnFlags & ~KnownSpawnFlagsMask) != 0 ||
            float.IsNaN(message.RemainingLifeTime) ||
            float.IsInfinity(message.RemainingLifeTime) ||
            message.RemainingLifeTime < 0f ||
            message.RemainingLifeTime > MaxDropLifeTimeSeconds ||
            (!message.HasLifeTime && message.RemainingLifeTime != 0f) ||
            (message.IsCatchUp && message.WorldItemId == Guid.Empty))
        {
            Logger.Error("[WeaponDrop] Rejected malformed drop message");
            return false;
        }

        if (!objectManager.TryGetObjectWithLogging(message.ItemObjectId, out ItemObject item))
            return false;

        ItemModifier modifier = null;
        if (!string.IsNullOrEmpty(message.ItemModifierId) &&
            !objectManager.TryGetObjectWithLogging(message.ItemModifierId, out modifier))
        {
            return false;
        }

        try
        {
            Banner banner = string.IsNullOrEmpty(message.BannerCode)
                ? null
                : new Banner(message.BannerCode);
            weapon = new MissionWeapon(item, modifier, banner, message.DataValue);
            if (weapon.IsEmpty)
            {
                Logger.Error(
                    "[WeaponDrop] Rejected item without a weapon component drop={DropId} item={ItemObjectId}",
                    message.DropId,
                    message.ItemObjectId);
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "[WeaponDrop] Failed to build canonical weapon for drop={DropId}", message.DropId);
            return false;
        }
    }

    private void RecordApplied(NetworkWeaponDropped message, SpawnedItemEntity item)
    {
        appliedDropIds.Add(message.DropId);
        if (message.WorldItemId == Guid.Empty || item == null) return;

        worldItemRegistry.Register(message.WorldItemId, item);
        activeDrops[message.WorldItemId] = message;
    }

    private void RetireWorldItem(Guid worldItemId)
    {
        retiredWorldItemIds.Add(worldItemId);
        activeDrops.Remove(worldItemId);
        remainingWorldItemAmounts.Remove(worldItemId);
        worldItemRegistry.Remove(worldItemId);
    }

    private void ScheduleObservedDropExpiry(
        (Guid AgentId, EquipmentIndex Slot) key,
        ObservedDrop observed)
    {
        GameThread.EnqueueSafe(
            () => CheckObservedDropExpiry(key, observed),
            context: nameof(CheckObservedDropExpiry));
    }

    private void CheckObservedDropExpiry(
        (Guid AgentId, EquipmentIndex Slot) key,
        ObservedDrop observed)
    {
        if (disposed ||
            !pendingDrops.TryGetValue(key, out Queue<ObservedDrop> queue) ||
            !queue.Contains(observed))
        {
            return;
        }

        if (DateTime.UtcNow < observed.ExpiresAtUtc)
        {
            ScheduleObservedDropExpiry(key, observed);
            return;
        }

        if (observed.HasPendingPickup) return;

        var retained = new Queue<ObservedDrop>();
        while (queue.Count > 0)
        {
            ObservedDrop candidate = queue.Dequeue();
            if (!ReferenceEquals(candidate, observed))
                retained.Enqueue(candidate);
        }
        if (retained.Count == 0)
            pendingDrops.Remove(key);
        else
            pendingDrops[key] = retained;

        AbandonObservedWorldItemIdentity(observed);
        if (observed.Item != null && worldItemSpawner.IsPresent(observed.Item))
            DiscardObservedDrop(observed, key.AgentId, key.Slot, "authoritative-timeout");

        CoopAgentInfo agentInfo = null;
        Agent agent = observed.Agent;
        if (networkAgentRegistry.TryGetAgentInfo(key.AgentId, out CoopAgentInfo registeredInfo) &&
            registeredInfo.Agent != null)
        {
            agentInfo = registeredInfo;
            agent = registeredInfo.Agent;
        }

        if (!observed.HasLaterAuthoritativeSlotTransition &&
            agent?.Equipment != null &&
            agent.Mission == Mission.Current &&
            agent.IsActive() &&
            IsValidEquipmentIndex(observed.EquipmentIndex))
        {
            using (new AllowedThread())
            {
                MissionWeapon current = agent.Equipment[observed.EquipmentIndex];
                if (!WeaponMatches(current, observed.Weapon))
                {
                    if (!current.IsEmpty)
                        agent.RemoveEquippedWeapon(observed.EquipmentIndex);
                    if (!observed.Weapon.IsEmpty)
                    {
                        MissionWeapon weapon = observed.Weapon;
                        agent.EquipWeaponWithNewEntity(observed.EquipmentIndex, ref weapon);
                    }
                }

                if (agentInfo != null &&
                    agentInfo.TryGetAuthoritativeEquipment(out AgentEquipmentData equipment))
                {
                    equipment.Apply(agent);
                }
            }
        }

        Logger.Warning(
            "[WeaponDrop] Rolled back unmatched observed drop agent={AgentId} slot={EquipmentIndex}",
            key.AgentId,
            key.Slot);
    }

    private void SendCatchUp(string controllerId)
    {
        int sent = 0;
        var snapshot = new List<KeyValuePair<Guid, NetworkWeaponDropped>>(activeDrops);
        foreach (KeyValuePair<Guid, NetworkWeaponDropped> pair in snapshot)
        {
            if (retiredWorldItemIds.Contains(pair.Key) ||
                !worldItemRegistry.TryGet(pair.Key, out SpawnedItemEntity item) ||
                !worldItemSpawner.IsPresent(item))
            {
                activeDrops.Remove(pair.Key);
                continue;
            }

            if (!TryCreateCatchUp(pair.Value, item, out NetworkWeaponDropped message))
                continue;

            network.Send(controllerId, message);
            sent++;
        }

        Logger.Debug(
            "[WeaponDrop] Sent {Count} active drop state(s) to joining controller={ControllerId}",
            sent,
            controllerId);
    }

    private static bool WeaponMatches(MissionWeapon current, MissionWeapon canonical) =>
        !current.IsEmpty &&
        current.Item == canonical.Item &&
        current.ItemModifier == canonical.ItemModifier &&
        current.RawDataForNetwork == canonical.RawDataForNetwork &&
        current.Banner?.Serialize() == canonical.Banner?.Serialize();

    private static bool IsValidEquipmentIndex(EquipmentIndex equipmentIndex) =>
        equipmentIndex >= EquipmentIndex.WeaponItemBeginSlot &&
        equipmentIndex < EquipmentIndex.NumAllWeaponSlots;
}
