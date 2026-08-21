using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>Synchronizes authoritative agent weapon drops and their runtime world-item identities.</summary>
public interface IWeaponDropHandler : IHandler
{
    void CatchUpJoiner(string controllerId);
    void ConfigureLocalHostProvider(Func<bool> provider);
    void Tick(float dt);
}

/// <inheritdoc cref="IWeaponDropHandler"/>
public class WeaponDropHandler : IWeaponDropHandler
{
    private const int MaxPendingDropsPerSlot = 8;
    private const int MaxAppliedDropIds = 512;
    private const int MaxConsumedDropIds = 512;
    private const int MaxRetiredWorldItemIds = 256;
    private const int MaxPreDropStates = 256;
    private const int MaxPendingResyncRequests = 64;
    private const int MaxResyncPickupIdsPerRequest = 512;
    private const int MaxWorldItemPickupIds = short.MaxValue + 1;
    private const int MaxObjectIdLength = 256;
    private const int MaxBannerCodeLength = 4096;
    private const int KnownSpawnFlagsMask = 0x7F;
    private const float MaxDropLifeTimeSeconds = 180f;
    private static readonly TimeSpan ObservedDropTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PreDropStateTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ResyncRequestTimeout = TimeSpan.FromSeconds(10);

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
        }
    }

    private sealed class WorldItemTransitionState
    {
        public long Revision { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsPreDrop { get; set; }
        public bool HasRemainingAmount { get; set; }
        public short RemainingAmount { get; set; }
        public HashSet<(Guid AgentId, EquipmentIndex Slot)> SlotTransitions { get; } =
            new HashSet<(Guid AgentId, EquipmentIndex Slot)>();
        public HashSet<Guid> PickupIds { get; } = new HashSet<Guid>();
        private Queue<Guid> PickupIdOrder { get; } = new Queue<Guid>();

        public void TrackPickupId(Guid pickupId)
        {
            if (pickupId == Guid.Empty || !PickupIds.Add(pickupId)) return;

            PickupIdOrder.Enqueue(pickupId);
            while (PickupIdOrder.Count > MaxWorldItemPickupIds)
                PickupIds.Remove(PickupIdOrder.Dequeue());
        }
    }

    private sealed class PendingResyncRequest
    {
        public NetworkWeaponDropResyncRequest Request { get; }
        public DateTime ExpiresAtUtc { get; }

        public PendingResyncRequest(NetworkWeaponDropResyncRequest request)
        {
            Request = request;
            ExpiresAtUtc = DateTime.UtcNow.Add(ResyncRequestTimeout);
        }
    }

    private readonly INetworkAgentRegistry networkAgentRegistry;
    private readonly INetworkWorldItemRegistry worldItemRegistry;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleNetwork network;
    private readonly IObjectManager objectManager;
    private readonly IWeaponDropWorldItemSpawner worldItemSpawner;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly Dictionary<(Guid AgentId, EquipmentIndex Slot), Queue<ObservedDrop>> pendingDrops =
        new Dictionary<(Guid AgentId, EquipmentIndex Slot), Queue<ObservedDrop>>();
    private readonly Dictionary<Guid, NetworkWeaponDropped> activeDrops =
        new Dictionary<Guid, NetworkWeaponDropped>();
    private readonly Dictionary<Guid, float> activeDropRemainingLifeTime =
        new Dictionary<Guid, float>();
    private readonly HashSet<Guid> appliedDropIds = new HashSet<Guid>();
    private readonly Queue<Guid> appliedDropIdOrder = new Queue<Guid>();
    private readonly HashSet<Guid> consumedDropIds = new HashSet<Guid>();
    private readonly Queue<Guid> consumedDropIdOrder = new Queue<Guid>();
    private readonly HashSet<Guid> consumedWorldItemIds = new HashSet<Guid>();
    private readonly HashSet<Guid> retiredWorldItemIds = new HashSet<Guid>();
    private readonly Queue<Guid> retiredWorldItemOrder = new Queue<Guid>();
    private readonly Dictionary<Guid, WorldItemTransitionState> worldItemTransitionStates =
        new Dictionary<Guid, WorldItemTransitionState>();
    private readonly Queue<Guid> preDropStateOrder = new Queue<Guid>();
    private readonly HashSet<Guid> liveDropAppliedWorldItemIds = new HashSet<Guid>();
    private readonly Dictionary<Guid, PendingResyncRequest> pendingResyncRequests =
        new Dictionary<Guid, PendingResyncRequest>();
    private readonly Queue<Guid> pendingResyncRequestOrder = new Queue<Guid>();
    private readonly CancellationTokenSource expiryCancellation = new CancellationTokenSource();
    private Func<bool> isLocalHost = () => false;
    private float pruneElapsedSeconds;
    private bool disposed;

    public WeaponDropHandler(
        INetworkAgentRegistry networkAgentRegistry,
        INetworkWorldItemRegistry worldItemRegistry,
        IMessageBroker messageBroker,
        IBattleNetwork network,
        IObjectManager objectManager,
        IWeaponDropWorldItemSpawner worldItemSpawner)
        : this(
            networkAgentRegistry,
            worldItemRegistry,
            messageBroker,
            network,
            objectManager,
            worldItemSpawner,
            null)
    {
    }

    public WeaponDropHandler(
        INetworkAgentRegistry networkAgentRegistry,
        INetworkWorldItemRegistry worldItemRegistry,
        IMessageBroker messageBroker,
        IBattleNetwork network,
        IObjectManager objectManager,
        IWeaponDropWorldItemSpawner worldItemSpawner,
        IControllerIdProvider controllerIdProvider)
    {
        this.networkAgentRegistry = networkAgentRegistry;
        this.worldItemRegistry = worldItemRegistry;
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.worldItemSpawner = worldItemSpawner;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<WeaponDropped>(HandleWeaponDropped);
        messageBroker.Subscribe<NetworkWeaponDropped>(HandleNetworkWeaponDropped);
        messageBroker.Subscribe<NetworkWeaponDropResyncRequest>(HandleNetworkWeaponDropResyncRequest);
        messageBroker.Subscribe<AcceptedWeaponDropStateResponse>(HandleAcceptedWeaponDropStateResponse);
        messageBroker.Subscribe<WeaponPickedup>(HandleWeaponPickedup);
        messageBroker.Subscribe<WeaponPickupApplied>(HandleWeaponPickupApplied);
        messageBroker.Subscribe<PendingWorldItemPickupsRejected>(HandlePendingWorldItemPickupsRejected);
    }

    ~WeaponDropHandler()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        expiryCancellation.Cancel();
        messageBroker.Unsubscribe<WeaponDropped>(HandleWeaponDropped);
        messageBroker.Unsubscribe<NetworkWeaponDropped>(HandleNetworkWeaponDropped);
        messageBroker.Unsubscribe<NetworkWeaponDropResyncRequest>(HandleNetworkWeaponDropResyncRequest);
        messageBroker.Unsubscribe<AcceptedWeaponDropStateResponse>(HandleAcceptedWeaponDropStateResponse);
        messageBroker.Unsubscribe<WeaponPickedup>(HandleWeaponPickedup);
        messageBroker.Unsubscribe<WeaponPickupApplied>(HandleWeaponPickupApplied);
        messageBroker.Unsubscribe<PendingWorldItemPickupsRejected>(HandlePendingWorldItemPickupsRejected);
        pendingDrops.Clear();
        activeDrops.Clear();
        activeDropRemainingLifeTime.Clear();
        appliedDropIds.Clear();
        appliedDropIdOrder.Clear();
        consumedDropIds.Clear();
        consumedDropIdOrder.Clear();
        consumedWorldItemIds.Clear();
        retiredWorldItemIds.Clear();
        retiredWorldItemOrder.Clear();
        worldItemTransitionStates.Clear();
        preDropStateOrder.Clear();
        liveDropAppliedWorldItemIds.Clear();
        pendingResyncRequests.Clear();
        pendingResyncRequestOrder.Clear();
        expiryCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    public void CatchUpJoiner(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        GameThread.RunSafe(
            () => SendCatchUp(controllerId),
            context: nameof(CatchUpJoiner));
    }

    public void ConfigureLocalHostProvider(Func<bool> provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));

        isLocalHost = provider;
    }

    public void Tick(float dt)
    {
        float elapsed = MathF.Max(0f, dt);
        AdvanceActiveDropLifeTimes(elapsed);
        pruneElapsedSeconds += elapsed;
        if (pruneElapsedSeconds < 1f) return;

        pruneElapsedSeconds = 0f;
        PruneUnavailableWorldItems();
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

        TrackAppliedDropId(message.DropId);
        if (message.WorldItemId != Guid.Empty && dropped.DroppedItem != null)
        {
            RecordActiveDrop(message);
            MarkLiveDropApplied(message.WorldItemId);
            RetryPendingResyncRequests(message.WorldItemId);
        }

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

    private void HandleNetworkWeaponDropResyncRequest(
        MessagePayload<NetworkWeaponDropResyncRequest> payload)
    {
        NetworkWeaponDropResyncRequest request = payload.What;
        GameThread.RunSafe(
            () => SendRequestedWorldItemState(request),
            context: nameof(HandleNetworkWeaponDropResyncRequest));
    }

    private void SendRequestedWorldItemState(NetworkWeaponDropResyncRequest request)
    {
        if (request == null ||
            request.WorldItemId == Guid.Empty ||
            string.IsNullOrEmpty(request.RequesterControllerId))
        {
            return;
        }

        if (isLocalHost())
        {
            if (TrySendWorldItemStateResponse(request))
            {
                pendingResyncRequests.Remove(request.RequestId);
                CompactPendingResyncRequestOrder();
            }
            else
            {
                QueuePendingResyncRequest(request);
            }
        }

        int requestedCount = Math.Min(
            Math.Min(
                request.AgentIds?.Length ?? 0,
                request.EquipmentIndices?.Length ?? 0),
            64);
        for (int i = 0; i < requestedCount; i++)
        {
            Guid agentId = request.AgentIds[i];
            EquipmentIndex equipmentIndex = request.EquipmentIndices[i];
            if (!networkAgentRegistry.TryGetAgentInfo(agentId, out CoopAgentInfo agentInfo) ||
                agentInfo.Agent == null ||
                !IsValidEquipmentIndex(equipmentIndex) ||
                !networkAgentRegistry.IsLocallyControlled(agentInfo.Agent))
            {
                continue;
            }

            MissionWeapon weapon = agentInfo.Agent.Equipment[equipmentIndex];
            string itemObjectId = null;
            string itemModifierId = null;
            if (!weapon.IsEmpty &&
                !TrySerializeWeapon(weapon, out itemObjectId, out itemModifierId))
            {
                continue;
            }
            SendToController(
                request.RequesterControllerId,
                new NetworkWeaponPickupSlotState(
                    agentId,
                    equipmentIndex,
                    itemObjectId,
                    itemModifierId,
                    weapon.Banner?.Serialize(),
                    weapon.RawDataForNetwork,
                    new AgentEquipmentData(agentInfo.Agent),
                    request.RequestId,
                    request.WorldItemId,
                    agentInfo.AuthorityRevision,
                    agentInfo.CurrentAuthority));
        }
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

        if (networkAgentRegistry.TryGetAgentInfo(
                pickedup.Agent,
                out CoopAgentInfo pickerInfo))
        {
            RecordWorldItemPickup(
                worldItemId,
                pickerInfo.AgentId,
                pickedup.EquipmentIndex,
                pickedup.PickupId,
                pickedup.ResultingWorldItemAmount,
                pickedup.WorldItemConsumed,
                slotTransitionApplied: true);
        }

        if (pickedup.WorldItemConsumed)
        {
            GameThread.EnqueueSafe(
                () => RetireWorldItem(worldItemId),
                context: nameof(HandleWeaponPickedup));
        }
        else
        {
            pickedup.WorldItem._weapon.Amount = pickedup.ResultingWorldItemAmount;
        }
    }

    private void HandleWeaponPickupApplied(MessagePayload<WeaponPickupApplied> payload)
    {
        WeaponPickupApplied applied = payload.What;
        if (applied.WorldItemId != Guid.Empty)
        {
            RecordWorldItemPickup(
                applied.WorldItemId,
                applied.AgentId,
                applied.EquipmentIndex,
                applied.PickupId,
                applied.ResultingWorldItemAmount,
                applied.WorldItemConsumed,
                applied.SlotTransitionApplied);

            if (applied.WorldItemConsumed)
                RetireWorldItem(applied.WorldItemId);
            else
            {
                if (worldItemRegistry.TryGet(
                        applied.WorldItemId,
                        out SpawnedItemEntity item) &&
                    worldItemSpawner.IsPresent(item))
                {
                    item._weapon.Amount = applied.ResultingWorldItemAmount;
                }
            }
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

    private void HandlePendingWorldItemPickupsRejected(
        MessagePayload<PendingWorldItemPickupsRejected> payload)
    {
        SpawnedItemEntity worldItem = payload.What.WorldItem;
        if (worldItem == null) return;

        foreach (KeyValuePair<(Guid AgentId, EquipmentIndex Slot), Queue<ObservedDrop>> pair
                 in new List<KeyValuePair<(Guid AgentId, EquipmentIndex Slot), Queue<ObservedDrop>>>(pendingDrops))
        {
            foreach (ObservedDrop observed in pair.Value)
            {
                if (!ReferenceEquals(observed.Item, worldItem)) continue;

                RejectObservedDrop(
                    pair.Key,
                    observed,
                    "pickup-identity-limit",
                    requireExpired: false);
                return;
            }
        }
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

        RecordActiveDropExpiry(source.WorldItemId, source.HasLifeTime, remainingLifeTime);

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

    private bool TryCreateStoredCatchUp(
        NetworkWeaponDropped source,
        out NetworkWeaponDropped message)
    {
        message = null;
        if (!TryBuildCanonicalWeapon(source, out MissionWeapon weapon)) return false;

        if (worldItemTransitionStates.TryGetValue(
                source.WorldItemId,
                out WorldItemTransitionState state) &&
            state.HasRemainingAmount)
        {
            weapon.Amount = state.RemainingAmount;
        }

        float remainingLifeTime = source.RemainingLifeTime;
        if (source.HasLifeTime)
        {
            if (!activeDropRemainingLifeTime.TryGetValue(
                    source.WorldItemId,
                    out remainingLifeTime))
                return false;
            if (remainingLifeTime <= 0f) return false;
        }

        message = new NetworkWeaponDropped(
            source.DropId,
            source.AgentId,
            source.EquipmentIndex,
            source.WorldItemId,
            source.OriginControllerId,
            source.ItemObjectId,
            source.ItemModifierId,
            weapon.Banner?.Serialize(),
            weapon.RawDataForNetwork,
            source.Position,
            source.Rotation,
            source.SpawnFlags,
            source.HasLifeTime,
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
            ObservedDrop discarded = queue.Dequeue();
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

        WorldItemTransitionState transitionState = null;
        if (message.WorldItemId != Guid.Empty &&
            worldItemTransitionStates.TryGetValue(
                message.WorldItemId,
                out transitionState) &&
            transitionState.HasRemainingAmount)
        {
            canonical.Amount = transitionState.RemainingAmount;
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

        bool hasAgent = networkAgentRegistry.TryGetAgentInfo(
            message.AgentId,
            out CoopAgentInfo agentInfo);
        if (!hasAgent)
            Logger.Warning("[WeaponDrop] Reconciling drop={DropId} without agent={AgentId}", message.DropId, message.AgentId);

        if (message.WorldItemId != Guid.Empty &&
            worldItemTransitionStates.TryGetValue(
                message.WorldItemId,
                out transitionState) &&
            transitionState.IsPreDrop &&
            (transitionState.SlotTransitions.Count > 0 ||
             transitionState.HasRemainingAmount) &&
            ApplyDropAfterPickup(
                message,
                ref canonical,
                hasAgent ? agentInfo : null,
                transitionState.SlotTransitions.Contains(
                    (message.AgentId, message.EquipmentIndex))))
        {
            return;
        }

        if (message.WorldItemId != Guid.Empty &&
            (retiredWorldItemIds.Contains(message.WorldItemId) ||
             consumedWorldItemIds.Contains(message.WorldItemId)))
        {
            bool hadRetiredObservation = TryTakeObservedDrop(
                message.AgentId,
                message.EquipmentIndex,
                canonical,
                expectsWorldItem: true,
                out ObservedDrop retiredObservation);
            if (hadRetiredObservation)
            {
                DiscardObservedDrop(
                    retiredObservation,
                    message.AgentId,
                    message.EquipmentIndex,
                    "retired-before-drop");
                ResolveObservedWorldItemIdentity(retiredObservation, message.WorldItemId);
            }
            if (hasAgent && transitionState != null && transitionState.IsPreDrop)
                ApplyRetiredDropTransition(message, agentInfo);
            TrackAppliedDropId(message.DropId);
            TrackConsumedDropId(message.DropId);
            MarkLiveDropApplied(message.WorldItemId);
            RetryPendingResyncRequests(message.WorldItemId);
            Logger.Debug(
                "[WeaponDrop] Applied retired drop transition={DropId} worldItem={WorldItemId}",
                message.DropId,
                message.WorldItemId);
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
                    if (hasAgent)
                        ApplyCurrentEquipment(message, agentInfo);
                    TrackAppliedDropId(message.DropId);
                    MarkLiveDropApplied(message.WorldItemId);
                    RetireWorldItem(message.WorldItemId);
                    ResolveObservedWorldItemIdentity(observedDrop, message.WorldItemId);
                    return;
                }

                canonical.Amount = observedDrop.PendingRemainingAmount;
            }

            if (!ReconcileObservedDrop(message, ref canonical, observedDrop, out SpawnedItemEntity observedItem))
                return;

            if (hasAgent)
                ApplyCurrentEquipment(message, agentInfo);
            RecordApplied(message, observedItem);
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

        if (!hasAgent)
        {
            if (registeredItem == null &&
                !TrySpawnCanonical(message, ref canonical, out registeredItem))
            {
                return;
            }

            RecordApplied(message, registeredItem);
            return;
        }

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
        if (message.WorldItemId == Guid.Empty ||
            retiredWorldItemIds.Contains(message.WorldItemId) ||
            consumedWorldItemIds.Contains(message.WorldItemId) ||
            consumedDropIds.Contains(message.DropId))
            return;

        if (TryGetRegisteredCanonical(message, canonical, out SpawnedItemEntity registeredItem, out bool blocked))
        {
            RecordApplied(message, registeredItem, recordTransition: false);
            return;
        }
        if (blocked) return;

        if (IsActiveDropExpired(message.WorldItemId))
        {
            RetireExpiredWorldItem(message.WorldItemId);
            return;
        }

        if (!TrySpawnCanonical(message, ref canonical, out SpawnedItemEntity spawnedItem)) return;

        RecordApplied(message, spawnedItem, recordTransition: false);
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

    private static void ApplyRetiredDropTransition(
        NetworkWeaponDropped message,
        CoopAgentInfo agentInfo)
    {
        Agent agent = agentInfo.Agent;
        if (agent?.Equipment == null) return;

        if (!agent.Equipment[message.EquipmentIndex].IsEmpty)
        {
            using (new AllowedThread())
                agent.RemoveEquippedWeapon(message.EquipmentIndex);
        }
        ApplyCurrentEquipment(message, agentInfo);
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

        if (worldItemSpawner.IsPresent(registered) && !worldItemSpawner.TryRemove(registered))
        {
            Logger.Error(
                "[WeaponDrop] Failed to remove mismatched registered item drop={DropId} worldItem={WorldItemId}",
                message.DropId,
                message.WorldItemId);
            blocked = true;
            return false;
        }
        worldItemRegistry.Remove(message.WorldItemId);

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

            if (observed == null)
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
        ObservedDrop matched = null;
        (Guid AgentId, EquipmentIndex Slot) matchedKey = default;
        foreach (KeyValuePair<(Guid AgentId, EquipmentIndex Slot), Queue<ObservedDrop>> pair
                 in pendingDrops)
        {
            foreach (ObservedDrop observed in pair.Value)
            {
                if (!ReferenceEquals(observed.Item, pickup.WorldItem)) continue;
                matched = observed;
                matchedKey = pair.Key;
                break;
            }
            if (matched != null) break;
        }

        if (matched == null) return;

        matched.RecordPendingPickup(pickup);
        if (matched.PendingPickupConsumed && DateTime.UtcNow >= matched.ExpiresAtUtc)
        {
            GameThread.EnqueueSafe(
                () => CheckObservedDropExpiry(matchedKey, matched),
                context: nameof(CheckObservedDropExpiry));
        }
    }

    private void ResolveObservedWorldItemIdentity(ObservedDrop observed, Guid worldItemId)
    {
        if (observed?.Item == null) return;
        if (worldItemId == Guid.Empty)
        {
            AbandonObservedWorldItemIdentity(observed);
            return;
        }
        messageBroker.Publish(this, new WorldItemIdentityResolved(observed.Item, worldItemId));
    }

    private void AbandonObservedWorldItemIdentity(ObservedDrop observed)
    {
        if (observed?.Item == null) return;
        messageBroker.Publish(
            this,
            new WorldItemIdentityAbandoned(observed.Item));
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

    private bool ApplyDropAfterPickup(
        NetworkWeaponDropped message,
        ref MissionWeapon canonical,
        CoopAgentInfo agentInfo,
        bool preserveSourceSlot)
    {
        if (retiredWorldItemIds.Contains(message.WorldItemId) ||
            consumedWorldItemIds.Contains(message.WorldItemId))
        {
            if (!preserveSourceSlot && agentInfo != null)
                ApplyRetiredDropTransition(message, agentInfo);
            TrackAppliedDropId(message.DropId);
            TrackConsumedDropId(message.DropId);
            MarkLiveDropApplied(message.WorldItemId);
            RetryPendingResyncRequests(message.WorldItemId);
            Logger.Debug(
                "[WeaponDrop] Preserved consumed pickup before drop={DropId} worldItem={WorldItemId}",
                message.DropId,
                message.WorldItemId);
            return true;
        }

        TryGetRegisteredCanonical(
            message,
            canonical,
            out SpawnedItemEntity registeredItem,
            out bool blocked);
        if (blocked) return false;
        if (!preserveSourceSlot && agentInfo != null)
        {
            if (!ApplyAuthoritativeTransition(
                    message,
                    agentInfo,
                    ref canonical,
                    registeredItem,
                    out registeredItem))
            {
                return false;
            }
        }
        else if (registeredItem == null &&
                 !TrySpawnCanonical(message, ref canonical, out registeredItem))
        {
            return false;
        }

        RecordApplied(message, registeredItem);
        Logger.Debug(
            "[WeaponDrop] Materialized drop after newer pickup transition={DropId} worldItem={WorldItemId}",
            message.DropId,
            message.WorldItemId);
        return true;
    }

    private void RecordApplied(
        NetworkWeaponDropped message,
        SpawnedItemEntity item,
        bool recordTransition = true)
    {
        if (recordTransition)
            TrackAppliedDropId(message.DropId);
        if (message.WorldItemId == Guid.Empty || item == null) return;

        worldItemRegistry.Register(message.WorldItemId, item);
        RecordActiveDrop(message);
        if (recordTransition)
            MarkLiveDropApplied(message.WorldItemId);
        else
            CompactPreDropStateOrder();
        messageBroker.Publish(this, new WorldItemIdentityResolved(item, message.WorldItemId));
        RetryPendingResyncRequests(message.WorldItemId);
    }

    private void RetireWorldItem(Guid worldItemId, bool retryPending = true)
    {
        if (worldItemId == Guid.Empty) return;

        Guid dropId = activeDrops.TryGetValue(
            worldItemId,
            out NetworkWeaponDropped drop)
                ? drop.DropId
                : worldItemId;
        TrackConsumedDropId(dropId);
        TrackConsumedWorldItemId(worldItemId);
        TrackRetiredWorldItem(worldItemId);
        if (worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity item) &&
            worldItemSpawner.IsPresent(item) &&
            !worldItemSpawner.TryRemove(item))
        {
            Logger.Error("[WeaponDrop] Failed to remove retired world item={WorldItemId}", worldItemId);
        }
        RemoveActiveWorldItemState(worldItemId, clearPickupTransitions: false);
        CompactPreDropStateOrder();
        if (retryPending)
            RetryPendingResyncRequests(worldItemId);
    }

    private void RetireExpiredWorldItem(Guid worldItemId, bool retryPending = true)
    {
        if (!consumedWorldItemIds.Contains(worldItemId) &&
            worldItemTransitionStates.TryGetValue(
                worldItemId,
                out WorldItemTransitionState state))
        {
            state.Revision++;
        }

        RetireWorldItem(worldItemId, retryPending);
    }

    private void RecordWorldItemPickup(
        Guid worldItemId,
        Guid agentId,
        EquipmentIndex equipmentIndex,
        Guid pickupId,
        short resultingWorldItemAmount,
        bool worldItemConsumed,
        bool slotTransitionApplied)
    {
        if (worldItemId == Guid.Empty) return;

        WorldItemTransitionState state = GetOrCreateWorldItemTransitionState(worldItemId);
        state.Revision++;
        state.TrackPickupId(pickupId);
        if (state.IsPreDrop && slotTransitionApplied && IsValidEquipmentIndex(equipmentIndex))
            state.SlotTransitions.Add((agentId, equipmentIndex));
        state.HasRemainingAmount = !worldItemConsumed;
        state.RemainingAmount = resultingWorldItemAmount;
        if (state.IsPreDrop)
            state.ExpiresAtUtc = DateTime.UtcNow.Add(PreDropStateTimeout);

        RetryPendingResyncRequests(worldItemId);
    }

    private WorldItemTransitionState GetOrCreateWorldItemTransitionState(Guid worldItemId)
    {
        if (worldItemTransitionStates.TryGetValue(
                worldItemId,
                out WorldItemTransitionState state))
        {
            return state;
        }

        bool isPreDrop = !liveDropAppliedWorldItemIds.Contains(worldItemId);
        state = new WorldItemTransitionState
        {
            Revision = 0,
            IsPreDrop = isPreDrop,
            ExpiresAtUtc = DateTime.UtcNow.Add(PreDropStateTimeout),
        };
        worldItemTransitionStates.Add(worldItemId, state);
        if (isPreDrop)
        {
            preDropStateOrder.Enqueue(worldItemId);
            TrimPreDropStates();
        }
        return state;
    }

    private void MarkLiveDropApplied(Guid worldItemId)
    {
        if (worldItemId == Guid.Empty) return;

        liveDropAppliedWorldItemIds.Add(worldItemId);
        WorldItemTransitionState state = GetOrCreateWorldItemTransitionState(worldItemId);
        if (state.Revision == 0)
            state.Revision = 1;
        bool wasPreDrop = state.IsPreDrop;
        state.IsPreDrop = false;
        state.SlotTransitions.Clear();
        if (wasPreDrop)
            CompactPreDropStateOrder();
    }

    private bool TrySendWorldItemStateResponse(NetworkWeaponDropResyncRequest request)
    {
        if (request.RequestId == Guid.Empty ||
            !worldItemTransitionStates.TryGetValue(
                request.WorldItemId,
                out WorldItemTransitionState state))
        {
            return false;
        }

        Guid[] requiredPickupIds = request.RequiredPickupIds ?? Array.Empty<Guid>();
        if (requiredPickupIds.Length > MaxResyncPickupIdsPerRequest) return false;

        if (IsActiveDropExpired(request.WorldItemId) &&
            !IsActiveDropPresent(request.WorldItemId))
            RetireExpiredWorldItem(request.WorldItemId, retryPending: false);

        bool consumed = retiredWorldItemIds.Contains(request.WorldItemId) ||
            consumedWorldItemIds.Contains(request.WorldItemId);
        if (!consumed)
        {
            foreach (Guid pickupId in requiredPickupIds)
            {
                if (pickupId != Guid.Empty && !state.PickupIds.Contains(pickupId))
                    return false;
            }
        }

        NetworkWeaponDropped drop = null;
        if (!consumed)
        {
            if (!activeDrops.TryGetValue(
                    request.WorldItemId,
                    out NetworkWeaponDropped source) ||
                !TryCreateAvailableCatchUp(source, out drop))
            {
                consumed = retiredWorldItemIds.Contains(request.WorldItemId) ||
                    consumedWorldItemIds.Contains(request.WorldItemId);
                if (!consumed) return false;
            }
        }

        SendToController(
            request.RequesterControllerId,
            new NetworkWeaponDropStateResponse(
                request.RequestId,
                request.WorldItemId,
                state.Revision,
                consumed,
                drop,
                requiredPickupIds));
        return true;
    }

    private bool TryCreateAvailableCatchUp(
        NetworkWeaponDropped source,
        out NetworkWeaponDropped message)
    {
        if (worldItemRegistry.TryGet(
                source.WorldItemId,
                out SpawnedItemEntity item) &&
            worldItemSpawner.IsPresent(item))
        {
            return TryCreateCatchUp(source, item, out message);
        }

        if (IsActiveDropExpired(source.WorldItemId))
        {
            RetireExpiredWorldItem(source.WorldItemId, retryPending: false);
            message = null;
            return false;
        }

        worldItemRegistry.Remove(source.WorldItemId);
        return TryCreateStoredCatchUp(source, out message);
    }

    private void SendToController<T>(string controllerId, T message) where T : IMessage
    {
        if (controllerIdProvider != null &&
            controllerId == controllerIdProvider.ControllerId)
        {
            messageBroker.Publish(this, message);
            return;
        }

        network.Send(controllerId, message);
    }

    private void QueuePendingResyncRequest(NetworkWeaponDropResyncRequest request)
    {
        if (request.RequestId == Guid.Empty) return;

        if (pendingResyncRequests.ContainsKey(request.RequestId))
        {
            pendingResyncRequests[request.RequestId] = new PendingResyncRequest(request);
            return;
        }

        while (pendingResyncRequests.Count >= MaxPendingResyncRequests &&
               pendingResyncRequestOrder.Count > 0)
        {
            pendingResyncRequests.Remove(pendingResyncRequestOrder.Dequeue());
        }

        pendingResyncRequests.Add(request.RequestId, new PendingResyncRequest(request));
        pendingResyncRequestOrder.Enqueue(request.RequestId);
    }

    private void RetryPendingResyncRequests(Guid worldItemId)
    {
        if (!isLocalHost() || pendingResyncRequests.Count == 0) return;

        var snapshot = new List<KeyValuePair<Guid, PendingResyncRequest>>(pendingResyncRequests);
        foreach (KeyValuePair<Guid, PendingResyncRequest> pair in snapshot)
        {
            if (pair.Value.Request.WorldItemId == worldItemId &&
                TrySendWorldItemStateResponse(pair.Value.Request))
            {
                pendingResyncRequests.Remove(pair.Key);
            }
        }
        CompactPendingResyncRequestOrder();
    }

    private void HandleAcceptedWeaponDropStateResponse(
        MessagePayload<AcceptedWeaponDropStateResponse> payload)
    {
        NetworkWeaponDropStateResponse response = payload.What.Response;
        if (response == null || response.WorldItemId == Guid.Empty) return;

        WorldItemTransitionState state = GetOrCreateWorldItemTransitionState(response.WorldItemId);
        state.Revision = Math.Max(state.Revision, response.StateRevision);
        foreach (Guid pickupId in response.IncludedPickupIds ?? Array.Empty<Guid>())
        {
            state.TrackPickupId(pickupId);
        }

        if (response.WorldItemConsumed)
            RetireWorldItem(response.WorldItemId);
        else if (response.Drop != null)
            ApplyNetworkDrop(response.Drop);
    }

    private void TrimPreDropStates()
    {
        while (CountPreDropStates() > MaxPreDropStates && preDropStateOrder.Count > 0)
        {
            Guid worldItemId = preDropStateOrder.Dequeue();
            if (worldItemTransitionStates.TryGetValue(
                    worldItemId,
                out WorldItemTransitionState state) &&
                state.IsPreDrop &&
                !activeDrops.ContainsKey(worldItemId) &&
                !consumedWorldItemIds.Contains(worldItemId) &&
                !state.HasRemainingAmount)
            {
                worldItemTransitionStates.Remove(worldItemId);
            }
        }
    }

    private int CountPreDropStates()
    {
        int count = 0;
        foreach (KeyValuePair<Guid, WorldItemTransitionState> pair in worldItemTransitionStates)
        {
            if (pair.Value.IsPreDrop &&
                !activeDrops.ContainsKey(pair.Key) &&
                !consumedWorldItemIds.Contains(pair.Key) &&
                !pair.Value.HasRemainingAmount) count++;
        }
        return count;
    }

    private void PrunePreDropStates()
    {
        DateTime now = DateTime.UtcNow;
        var snapshot = new List<KeyValuePair<Guid, WorldItemTransitionState>>(
            worldItemTransitionStates);
        foreach (KeyValuePair<Guid, WorldItemTransitionState> pair in snapshot)
        {
            if (pair.Value.IsPreDrop &&
                !activeDrops.ContainsKey(pair.Key) &&
                !consumedWorldItemIds.Contains(pair.Key) &&
                !pair.Value.HasRemainingAmount &&
                pair.Value.ExpiresAtUtc <= now)
                worldItemTransitionStates.Remove(pair.Key);
        }
        CompactPreDropStateOrder();
    }

    private void PrunePendingResyncRequests()
    {
        DateTime now = DateTime.UtcNow;
        var snapshot = new List<KeyValuePair<Guid, PendingResyncRequest>>(pendingResyncRequests);
        foreach (KeyValuePair<Guid, PendingResyncRequest> pair in snapshot)
        {
            if (pair.Value.ExpiresAtUtc <= now)
                pendingResyncRequests.Remove(pair.Key);
        }
        CompactPendingResyncRequestOrder();
    }

    private void CompactPreDropStateOrder()
    {
        if (preDropStateOrder.Count == 0) return;

        var retained = new Queue<Guid>();
        var seen = new HashSet<Guid>();
        while (preDropStateOrder.Count > 0)
        {
            Guid worldItemId = preDropStateOrder.Dequeue();
            if (seen.Add(worldItemId) &&
                worldItemTransitionStates.TryGetValue(
                    worldItemId,
                    out WorldItemTransitionState state) &&
                state.IsPreDrop &&
                !activeDrops.ContainsKey(worldItemId) &&
                !consumedWorldItemIds.Contains(worldItemId) &&
                !state.HasRemainingAmount)
            {
                retained.Enqueue(worldItemId);
            }
        }
        while (retained.Count > 0)
            preDropStateOrder.Enqueue(retained.Dequeue());
    }

    private void CompactPendingResyncRequestOrder()
    {
        if (pendingResyncRequestOrder.Count == 0) return;

        var retained = new Queue<Guid>();
        var seen = new HashSet<Guid>();
        while (pendingResyncRequestOrder.Count > 0)
        {
            Guid requestId = pendingResyncRequestOrder.Dequeue();
            if (seen.Add(requestId) && pendingResyncRequests.ContainsKey(requestId))
                retained.Enqueue(requestId);
        }
        while (retained.Count > 0)
            pendingResyncRequestOrder.Enqueue(retained.Dequeue());
    }

    private void TrackRetiredWorldItem(Guid worldItemId)
    {
        if (!retiredWorldItemIds.Add(worldItemId)) return;

        retiredWorldItemOrder.Enqueue(worldItemId);
        while (retiredWorldItemOrder.Count > MaxRetiredWorldItemIds)
        {
            Guid expiredId = retiredWorldItemOrder.Dequeue();
            retiredWorldItemIds.Remove(expiredId);
        }
    }

    private void TrackAppliedDropId(Guid dropId)
    {
        if (dropId == Guid.Empty || !appliedDropIds.Add(dropId)) return;

        appliedDropIdOrder.Enqueue(dropId);
        while (appliedDropIdOrder.Count > MaxAppliedDropIds)
            appliedDropIds.Remove(appliedDropIdOrder.Dequeue());
    }

    private void TrackConsumedDropId(Guid dropId)
    {
        if (dropId == Guid.Empty || !consumedDropIds.Add(dropId)) return;

        consumedDropIdOrder.Enqueue(dropId);
        while (consumedDropIdOrder.Count > MaxConsumedDropIds)
            consumedDropIds.Remove(consumedDropIdOrder.Dequeue());
    }

    private void TrackConsumedWorldItemId(Guid worldItemId)
    {
        if (worldItemId != Guid.Empty)
            consumedWorldItemIds.Add(worldItemId);
    }

    private void RemoveActiveWorldItemState(
        Guid worldItemId,
        bool clearPickupTransitions = true)
    {
        activeDrops.Remove(worldItemId);
        activeDropRemainingLifeTime.Remove(worldItemId);
        if (clearPickupTransitions)
        {
            worldItemTransitionStates.Remove(worldItemId);
            liveDropAppliedWorldItemIds.Remove(worldItemId);
        }
        worldItemRegistry.Remove(worldItemId);
    }

    private void PruneUnavailableWorldItems()
    {
        PrunePreDropStates();
        PrunePendingResyncRequests();
        var snapshot = new List<KeyValuePair<Guid, NetworkWeaponDropped>>(activeDrops);
        foreach (KeyValuePair<Guid, NetworkWeaponDropped> pair in snapshot)
        {
            if (retiredWorldItemIds.Contains(pair.Key) ||
                consumedWorldItemIds.Contains(pair.Key))
            {
                RemoveActiveWorldItemState(pair.Key, clearPickupTransitions: false);
                continue;
            }

            if (worldItemRegistry.TryGet(pair.Key, out SpawnedItemEntity item) &&
                worldItemSpawner.IsPresent(item))
            {
                continue;
            }

            if (IsActiveDropExpired(pair.Key))
            {
                RetireExpiredWorldItem(pair.Key);
                continue;
            }

            worldItemRegistry.Remove(pair.Key);
        }
    }

    private void RecordActiveDrop(NetworkWeaponDropped message)
    {
        activeDrops[message.WorldItemId] = message;
        RecordActiveDropExpiry(
            message.WorldItemId,
            message.HasLifeTime,
            message.RemainingLifeTime);
    }

    private void RecordActiveDropExpiry(
        Guid worldItemId,
        bool hasLifeTime,
        float remainingLifeTime)
    {
        if (!hasLifeTime)
            return;

        float candidateRemainingLifeTime = MathF.Max(0f, remainingLifeTime);
        if (!activeDropRemainingLifeTime.TryGetValue(
                worldItemId,
                out float existingRemainingLifeTime) ||
            candidateRemainingLifeTime < existingRemainingLifeTime)
        {
            activeDropRemainingLifeTime[worldItemId] = candidateRemainingLifeTime;
        }
    }

    private bool IsActiveDropExpired(Guid worldItemId) =>
        activeDropRemainingLifeTime.TryGetValue(
            worldItemId,
            out float remainingLifeTime) &&
        remainingLifeTime <= 0f;

    private bool IsActiveDropPresent(Guid worldItemId) =>
        worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity item) &&
        worldItemSpawner.IsPresent(item);

    private void AdvanceActiveDropLifeTimes(float elapsed)
    {
        if (elapsed <= 0f || activeDropRemainingLifeTime.Count == 0) return;

        var worldItemIds = new List<Guid>(activeDropRemainingLifeTime.Keys);
        foreach (Guid worldItemId in worldItemIds)
        {
            activeDropRemainingLifeTime[worldItemId] =
                MathF.Max(0f, activeDropRemainingLifeTime[worldItemId] - elapsed);
        }
    }

    private void ScheduleObservedDropExpiry(
        (Guid AgentId, EquipmentIndex Slot) key,
        ObservedDrop observed)
    {
        CancellationToken cancellationToken = expiryCancellation.Token;
        _ = ScheduleObservedDropExpiryAsync(key, observed, cancellationToken);
    }

    private async Task ScheduleObservedDropExpiryAsync(
        (Guid AgentId, EquipmentIndex Slot) key,
        ObservedDrop observed,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ObservedDropTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested) return;
        GameThread.EnqueueSafe(
            () => CheckObservedDropExpiry(key, observed),
            context: nameof(CheckObservedDropExpiry));
    }

    private void CheckObservedDropExpiry(
        (Guid AgentId, EquipmentIndex Slot) key,
        ObservedDrop observed)
    {
        RejectObservedDrop(
            key,
            observed,
            "authoritative-timeout",
            requireExpired: true);
    }

    private void RejectObservedDrop(
        (Guid AgentId, EquipmentIndex Slot) key,
        ObservedDrop observed,
        string reason,
        bool requireExpired)
    {
        if (disposed ||
            !pendingDrops.TryGetValue(key, out Queue<ObservedDrop> queue) ||
            !queue.Contains(observed))
        {
            return;
        }

        if (requireExpired && DateTime.UtcNow < observed.ExpiresAtUtc) return;
        if (requireExpired &&
            observed.HasPendingPickup &&
            !observed.PendingPickupConsumed)
        {
            return;
        }

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
            DiscardObservedDrop(observed, key.AgentId, key.Slot, reason);

        CoopAgentInfo agentInfo = null;
        Agent agent = observed.Agent;
        if (networkAgentRegistry.TryGetAgentInfo(key.AgentId, out CoopAgentInfo registeredInfo) &&
            registeredInfo.Agent != null)
        {
            agentInfo = registeredInfo;
            agent = registeredInfo.Agent;
        }

        if (!observed.HasLaterAuthoritativeSlotTransition &&
            !networkAgentRegistry.IsLocallyControlled(key.AgentId) &&
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
            "[WeaponDrop] Rolled back unmatched observed drop agent={AgentId} slot={EquipmentIndex} reason={Reason}",
            key.AgentId,
            key.Slot,
            reason);
    }

    private void SendCatchUp(string controllerId)
    {
        int sent = 0;
        var snapshot = new List<KeyValuePair<Guid, NetworkWeaponDropped>>(activeDrops);
        foreach (KeyValuePair<Guid, NetworkWeaponDropped> pair in snapshot)
        {
            if (retiredWorldItemIds.Contains(pair.Key) ||
                consumedWorldItemIds.Contains(pair.Key))
            {
                RemoveActiveWorldItemState(pair.Key, clearPickupTransitions: false);
                continue;
            }

            if (!TryCreateAvailableCatchUp(pair.Value, out NetworkWeaponDropped message))
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
