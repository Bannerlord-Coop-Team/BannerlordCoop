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
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers
{
    /// <summary>
    /// Handler for weapon pickups within a battle
    /// </summary>
    public interface IWeaponPickupHandler : IHandler
    {
        void Tick(float dt);
    }
    /// <inheritdoc/>
    public class WeaponPickupHandler : IWeaponPickupHandler
    {
        private const int MaxPendingNetworkPickupsPerWorldItem = 8;
        private const int MaxPendingNetworkWorldItems = 64;
        private const int MaxPendingIdentityPickupsPerWorldItem = 8;
        private const int MaxResolvedPickupIds = 512;
        private const int MaxResyncTargetsPerMessage = 64;
        private const int MaxResyncPickupIdsPerMessage = 512;
        private const float ResyncRetrySeconds = 5f;
        private const float ResyncGraceRetrySeconds = 1f;
        private const float ResyncCompletionGraceSeconds = 5f;
        private const float MissingResyncTargetTimeoutSeconds = 5f;

        /// <summary>Retains the authoritative picker id while a runtime item waits for its canonical id.</summary>
        private readonly struct PendingIdentityPickup
        {
            public Guid AgentId { get; }
            public WeaponPickedup Pickup { get; }

            public PendingIdentityPickup(Guid agentId, WeaponPickedup pickup)
            {
                AgentId = agentId;
                Pickup = pickup;
            }
        }

        private sealed class PendingNetworkPickup
        {
            public NetworkWeaponPickedup Message { get; }

            public PendingNetworkPickup(NetworkWeaponPickedup message)
            {
                Message = message;
            }
        }

        private sealed class ResyncRequestState
        {
            public Guid WorldItemId { get; }
            public string RequesterControllerId { get; }
            public Guid RequestId { get; } = Guid.NewGuid();
            public HashSet<(Guid AgentId, EquipmentIndex Slot)> AllTargets { get; } =
                new HashSet<(Guid AgentId, EquipmentIndex Slot)>();
            public HashSet<(Guid AgentId, EquipmentIndex Slot)> PendingTargets { get; } =
                new HashSet<(Guid AgentId, EquipmentIndex Slot)>();
            public HashSet<Guid> RequiredPickupIds { get; } = new HashSet<Guid>();
            public Dictionary<(Guid AgentId, EquipmentIndex Slot), float> MissingTargetSeconds { get; } =
                new Dictionary<(Guid AgentId, EquipmentIndex Slot), float>();
            public NetworkWeaponDropStateResponse PendingWorldResponse { get; set; }
            public long PublishedWorldStateRevision { get; set; } = long.MinValue;
            public bool WorldStateAccepted { get; set; }
            public bool CompletionPending { get; set; }
            public float CompletionElapsedSeconds { get; set; }
            public float RetryElapsedSeconds { get; set; }

            public ResyncRequestState(Guid worldItemId, string requesterControllerId)
            {
                WorldItemId = worldItemId;
                RequesterControllerId = requesterControllerId;
            }

            public void Merge(
                HashSet<(Guid AgentId, EquipmentIndex Slot)> targets,
                HashSet<Guid> pickupIds)
            {
                bool changed = false;
                foreach ((Guid agentId, EquipmentIndex slot) in targets)
                {
                    var target = (agentId, slot);
                    changed |= AllTargets.Add(target);
                    if (PendingTargets.Add(target))
                    {
                        MissingTargetSeconds.Remove(target);
                        changed = true;
                    }
                }
                foreach (Guid pickupId in pickupIds)
                {
                    if (RequiredPickupIds.Add(pickupId))
                    {
                        changed = true;
                        WorldStateAccepted = false;
                    }
                }
                if (!changed) return;

                CompletionPending = false;
                CompletionElapsedSeconds = 0f;
            }

            public bool MergeWorldResponse(NetworkWeaponDropStateResponse response)
            {
                if (response == null) return false;
                if (PendingWorldResponse == null ||
                    response.StateRevision > PendingWorldResponse.StateRevision)
                {
                    PendingWorldResponse = response;
                    return true;
                }
                if (response.StateRevision < PendingWorldResponse.StateRevision)
                    return false;

                var pickupIds = new HashSet<Guid>(
                    PendingWorldResponse.IncludedPickupIds ?? Array.Empty<Guid>());
                int previousCount = pickupIds.Count;
                pickupIds.UnionWith(response.IncludedPickupIds ?? Array.Empty<Guid>());
                bool changed = pickupIds.Count != previousCount;
                NetworkWeaponDropped drop = PendingWorldResponse.Drop ?? response.Drop;
                changed |= !ReferenceEquals(drop, PendingWorldResponse.Drop);
                if (!changed) return false;

                var mergedPickupIds = new Guid[pickupIds.Count];
                pickupIds.CopyTo(mergedPickupIds);

                PendingWorldResponse = new NetworkWeaponDropStateResponse(
                    response.RequestId,
                    response.WorldItemId,
                    response.StateRevision,
                    response.WorldItemConsumed,
                    drop,
                    mergedPickupIds,
                    response.HasRemainingAmount,
                    response.RemainingAmount);
                return true;
            }

            public List<NetworkWeaponDropResyncRequest> CreateRequests(bool includeAnsweredTargets)
            {
                var targets = new List<(Guid AgentId, EquipmentIndex Slot)>(
                    includeAnsweredTargets ? AllTargets : PendingTargets);
                var pickupIds = new List<Guid>(RequiredPickupIds);
                int targetMessageCount = Math.Max(
                    1,
                    (targets.Count + MaxResyncTargetsPerMessage - 1) /
                    MaxResyncTargetsPerMessage);
                int pickupMessageCount = Math.Max(
                    1,
                    (pickupIds.Count + MaxResyncPickupIdsPerMessage - 1) /
                    MaxResyncPickupIdsPerMessage);
                int messageCount = Math.Max(targetMessageCount, pickupMessageCount);
                var requests = new List<NetworkWeaponDropResyncRequest>(messageCount);
                for (int messageIndex = 0; messageIndex < messageCount; messageIndex++)
                {
                    int targetOffset = messageIndex * MaxResyncTargetsPerMessage;
                    int targetCount = Math.Min(
                        MaxResyncTargetsPerMessage,
                        Math.Max(0, targets.Count - targetOffset));
                    var agentIds = new Guid[targetCount];
                    var equipmentIndices = new EquipmentIndex[targetCount];
                    for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
                    {
                        (Guid agentId, EquipmentIndex slot) = targets[targetOffset + targetIndex];
                        agentIds[targetIndex] = agentId;
                        equipmentIndices[targetIndex] = slot;
                    }

                    int pickupOffset = messageIndex * MaxResyncPickupIdsPerMessage;
                    int pickupCount = Math.Min(
                        MaxResyncPickupIdsPerMessage,
                        Math.Max(0, pickupIds.Count - pickupOffset));
                    var requiredPickupIds = new Guid[pickupCount];
                    if (pickupCount > 0)
                        pickupIds.CopyTo(pickupOffset, requiredPickupIds, 0, pickupCount);

                    requests.Add(
                        new NetworkWeaponDropResyncRequest(
                            WorldItemId,
                            RequesterControllerId,
                            agentIds,
                            equipmentIndices,
                            RequestId,
                            requiredPickupIds));
                }
                return requests;
            }
        }

        readonly INetworkAgentRegistry networkAgentRegistry;
        readonly INetworkWorldItemRegistry worldItemRegistry;
        readonly IBattleNetwork network;
        readonly IMessageBroker messageBroker;
        readonly IObjectManager objectManager;
        readonly IControllerIdProvider controllerIdProvider;
        readonly Dictionary<SpawnedItemEntity, Queue<PendingIdentityPickup>> pendingIdentityPickups =
            new Dictionary<SpawnedItemEntity, Queue<PendingIdentityPickup>>();
        readonly HashSet<SpawnedItemEntity> pendingWorldItemIdentities =
            new HashSet<SpawnedItemEntity>();
        readonly Dictionary<Guid, Queue<PendingNetworkPickup>> pendingNetworkPickups =
            new Dictionary<Guid, Queue<PendingNetworkPickup>>();
        readonly Dictionary<Guid, ResyncRequestState> latestResyncRequests =
            new Dictionary<Guid, ResyncRequestState>();
        readonly Queue<Guid> resyncRequestOrder = new Queue<Guid>();
        readonly Dictionary<Guid, long> appliedWorldStateRevisions = new Dictionary<Guid, long>();
        readonly Dictionary<(Guid AgentId, EquipmentIndex Slot), long> appliedSlotStateRevisions =
            new Dictionary<(Guid AgentId, EquipmentIndex Slot), long>();
        readonly HashSet<Guid> resolvedPickupIds = new HashSet<Guid>();
        readonly Queue<Guid> resolvedPickupIdOrder = new Queue<Guid>();
        readonly static ILogger Logger = LogManager.GetLogger<WeaponPickupHandler>();
        bool disposed;
        public WeaponPickupHandler(
            INetworkAgentRegistry networkAgentRegistry,
            INetworkWorldItemRegistry worldItemRegistry,
            IBattleNetwork network,
            IMessageBroker messageBroker,
            IObjectManager objectManager)
            : this(
                networkAgentRegistry,
                worldItemRegistry,
                network,
                messageBroker,
                objectManager,
                null)
        {
        }

        public WeaponPickupHandler(
            INetworkAgentRegistry networkAgentRegistry,
            INetworkWorldItemRegistry worldItemRegistry,
            IBattleNetwork network,
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            IControllerIdProvider controllerIdProvider)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.worldItemRegistry = worldItemRegistry;
            this.network = network;
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.controllerIdProvider = controllerIdProvider;

            messageBroker.Subscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Subscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
            messageBroker.Subscribe<NetworkWeaponPickupSlotState>(HandleNetworkWeaponPickupSlotState);
            messageBroker.Subscribe<NetworkWeaponDropStateResponse>(HandleNetworkWeaponDropStateResponse);
            messageBroker.Subscribe<WorldItemIdentityResolved>(HandleWorldItemIdentityResolved);
            messageBroker.Subscribe<WorldItemIdentityPending>(HandleWorldItemIdentityPending);
            messageBroker.Subscribe<WorldItemIdentityAbandoned>(HandleWorldItemIdentityAbandoned);
        }
        ~WeaponPickupHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            messageBroker.Unsubscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Unsubscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
            messageBroker.Unsubscribe<NetworkWeaponPickupSlotState>(HandleNetworkWeaponPickupSlotState);
            messageBroker.Unsubscribe<NetworkWeaponDropStateResponse>(HandleNetworkWeaponDropStateResponse);
            messageBroker.Unsubscribe<WorldItemIdentityResolved>(HandleWorldItemIdentityResolved);
            messageBroker.Unsubscribe<WorldItemIdentityPending>(HandleWorldItemIdentityPending);
            messageBroker.Unsubscribe<WorldItemIdentityAbandoned>(HandleWorldItemIdentityAbandoned);
            pendingIdentityPickups.Clear();
            pendingWorldItemIdentities.Clear();
            pendingNetworkPickups.Clear();
            latestResyncRequests.Clear();
            resyncRequestOrder.Clear();
            appliedWorldStateRevisions.Clear();
            appliedSlotStateRevisions.Clear();
            resolvedPickupIds.Clear();
            resolvedPickupIdOrder.Clear();
            GC.SuppressFinalize(this);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            RetryPendingNetworkPickupsWithActiveAgents();
            if (latestResyncRequests.Count == 0) return;

            var snapshot = new List<ResyncRequestState>(latestResyncRequests.Values);
            foreach (ResyncRequestState state in snapshot)
            {
                if (!latestResyncRequests.TryGetValue(
                        state.WorldItemId,
                        out ResyncRequestState current) ||
                    current != state)
                {
                    continue;
                }
                UpdateUnavailableResyncTargets(state, dt);
                TryCompleteResyncRequest(state.WorldItemId, state);
                if (state.CompletionPending)
                {
                    state.CompletionElapsedSeconds += dt;
                    if (state.CompletionElapsedSeconds >= ResyncCompletionGraceSeconds)
                    {
                        RemoveResyncRequest(state.WorldItemId);
                        continue;
                    }

                    state.RetryElapsedSeconds += dt;
                    if (state.RetryElapsedSeconds >= ResyncGraceRetrySeconds)
                    {
                        state.RetryElapsedSeconds = 0f;
                        SendResyncRequests(state, includeAnsweredTargets: true);
                    }
                    continue;
                }

                state.RetryElapsedSeconds += dt;
                if (state.RetryElapsedSeconds < ResyncRetrySeconds) continue;

                state.RetryElapsedSeconds = 0f;
                SendResyncRequests(state, includeAnsweredTargets: false);
            }
        }

        private void WeaponPickupSend(MessagePayload<WeaponPickedup> obj)
        {
            var payload = obj.What;

            if (!networkAgentRegistry.IsLocallyControlled(payload.Agent))
                return;

            if (!networkAgentRegistry.TryGetAgentInfo(payload.Agent, out var agentInfo))
            {
                Logger.Warning("No agentID was found for the Agent: {agent}", payload.Agent);
                return;
            }

            if (payload.WorldItem == null)
            {
                Logger.Error("No world item was found for a weapon pickup");
                return;
            }

            Guid worldItemId;
            if (payload.WorldItem.Id.CreatedAtRuntime &&
                !worldItemRegistry.TryGetId(payload.WorldItem, out worldItemId))
            {
                if (!pendingWorldItemIdentities.Contains(payload.WorldItem))
                {
                    RejectWeaponPickup(
                        new PendingIdentityPickup(agentInfo.AgentId, payload),
                        "uncorrelated-runtime-identity");
                    return;
                }

                if (!pendingIdentityPickups.TryGetValue(
                        payload.WorldItem,
                        out Queue<PendingIdentityPickup> pending))
                {
                    pending = new Queue<PendingIdentityPickup>();
                    pendingIdentityPickups.Add(payload.WorldItem, pending);
                }
                pending.Enqueue(new PendingIdentityPickup(agentInfo.AgentId, payload));
                if (pending.Count > MaxPendingIdentityPickupsPerWorldItem)
                {
                    pendingIdentityPickups.Remove(payload.WorldItem);
                    RejectPendingIdentityPickups(pending, "pending-identity-limit");
                    messageBroker.Publish(
                        this,
                        new PendingWorldItemPickupsRejected(payload.WorldItem));
                    return;
                }
                Logger.Debug("Deferred weapon pickup until the runtime world item receives its canonical identity");
                return;
            }
            if (!payload.WorldItem.Id.CreatedAtRuntime)
                worldItemId = worldItemRegistry.GetOrCreateId(payload.WorldItem);

            SendWeaponPickup(payload, agentInfo.AgentId, worldItemId);
        }

        private void SendWeaponPickup(
            WeaponPickedup payload,
            Guid agentId,
            Guid worldItemId,
            bool isIdentityCorrection = false)
        {
            if (!objectManager.TryGetIdWithLogging(payload.WeaponObject, out string itemObjectId))
                return;
            string worldItemModifierId = null;
            if (payload.WeaponModifier != null &&
                !objectManager.TryGetIdWithLogging(payload.WeaponModifier, out worldItemModifierId))
            {
                return;
            }
            if (payload.ResultingSlotWeapon.IsEmpty ||
                !objectManager.TryGetIdWithLogging(
                    payload.ResultingSlotWeapon.Item,
                    out string resultingSlotItemObjectId))
            {
                Logger.Error("No resulting slot weapon was found for a weapon pickup");
                return;
            }
            string resultingSlotItemModifierId = null;
            if (payload.ResultingSlotWeapon.ItemModifier != null &&
                !objectManager.TryGetIdWithLogging(
                    payload.ResultingSlotWeapon.ItemModifier,
                    out resultingSlotItemModifierId))
            {
                return;
            }
            MissionWeapon originalWorldItemWeapon = payload.WorldItem.WeaponCopy;
            originalWorldItemWeapon.Amount = payload.PreviousWorldItemAmount;
            Guid pickupId = payload.PickupId == Guid.Empty
                ? Guid.NewGuid()
                : payload.PickupId;

            NetworkWeaponPickedup message = new NetworkWeaponPickedup(
                agentId,
                payload.EquipmentIndex,
                worldItemId,
                itemObjectId,
                payload.WeaponModifier,
                payload.Banner,
                payload.CurrentEquipment,
                payload.PreviousSlotAmount,
                payload.PreviousWorldItemAmount,
                payload.ResultingSlotAmount,
                payload.ResultingWorldItemAmount,
                payload.WorldItemConsumed,
                resultingSlotItemObjectId,
                resultingSlotItemModifierId,
                payload.ResultingSlotWeapon.Banner,
                payload.ResultingSlotWeapon.RawDataForNetwork,
                isIdentityCorrection,
                originalWorldItemWeapon.RawDataForNetwork,
                hasWorldItemDataValue: true,
                worldItemModifierId: worldItemModifierId,
                pickupId: pickupId);

            network.SendAll(message);
        }

        private void HandleWorldItemIdentityResolved(MessagePayload<WorldItemIdentityResolved> payload)
        {
            WorldItemIdentityResolved resolved = payload.What;
            if (resolved.WorldItemId == Guid.Empty) return;

            if (resolved.WorldItem != null)
            {
                pendingWorldItemIdentities.Remove(resolved.WorldItem);
                if (pendingIdentityPickups.TryGetValue(
                        resolved.WorldItem,
                        out Queue<PendingIdentityPickup> pending))
                {
                    pendingIdentityPickups.Remove(resolved.WorldItem);
                    while (pending.Count > 0)
                    {
                        PendingIdentityPickup pendingPickup = pending.Dequeue();
                        SendWeaponPickup(
                            pendingPickup.Pickup,
                            pendingPickup.AgentId,
                            resolved.WorldItemId);
                    }
                }
            }

            RetryPendingNetworkPickups(resolved.WorldItemId);
        }

        private void HandleWorldItemIdentityPending(MessagePayload<WorldItemIdentityPending> payload)
        {
            if (payload.What.WorldItem != null)
                pendingWorldItemIdentities.Add(payload.What.WorldItem);
        }

        private void HandleWorldItemIdentityAbandoned(MessagePayload<WorldItemIdentityAbandoned> payload)
        {
            SpawnedItemEntity worldItem = payload.What.WorldItem;
            if (worldItem == null) return;

            pendingWorldItemIdentities.Remove(worldItem);
            if (!pendingIdentityPickups.TryGetValue(
                    worldItem,
                    out Queue<PendingIdentityPickup> pending))
            {
                return;
            }

            pendingIdentityPickups.Remove(worldItem);
            RejectPendingIdentityPickups(pending, "world-item-identity-abandoned");
        }
        private void WeaponPickupReceive(MessagePayload<NetworkWeaponPickedup> obj)
        {
            NetworkWeaponPickedup message = obj.What;
            GameThread.RunSafe(() => ApplyOrQueueNetworkPickup(message));
        }

        private void ApplyOrQueueNetworkPickup(NetworkWeaponPickedup message)
        {
            if (message.PickupId != Guid.Empty && resolvedPickupIds.Contains(message.PickupId))
                return;

            if (message.IsIdentityCorrection)
            {
                messageBroker.Publish(
                    this,
                    new WeaponPickupApplied(
                        message.AgentId,
                        message.EquipmentIndex,
                        message.WorldItemId,
                        message.ResultingWorldItemAmount,
                        message.WorldItemConsumed,
                        slotTransitionApplied: false,
                        pickupId: message.PickupId));
                TrackResolvedPickup(message.PickupId);
                if (!message.WorldItemConsumed)
                    RetryPendingNetworkPickups(message.WorldItemId);
                return;
            }

            if (message.WorldItemId == Guid.Empty)
            {
                Logger.Warning(
                    "Ignored weapon pickup without canonical world item agent={AgentId} slot={EquipmentIndex}",
                    message.AgentId,
                    message.EquipmentIndex);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(message.ItemObjectId, out var itemObject))
                return;

            ItemModifier worldItemModifier = message.ItemModifier;
            if (!string.IsNullOrEmpty(message.WorldItemModifierId) &&
                !objectManager.TryGetObjectWithLogging<ItemModifier>(
                    message.WorldItemModifierId,
                    out worldItemModifier))
            {
                return;
            }
            MissionWeapon missionWeapon = new MissionWeapon(
                itemObject,
                worldItemModifier,
                message.Banner,
                message.WorldItemDataValue);
            MissionWeapon resultingSlotWeapon = missionWeapon;
            if (!string.IsNullOrEmpty(message.ResultingSlotItemObjectId))
            {
                if (!objectManager.TryGetObjectWithLogging<ItemObject>(
                        message.ResultingSlotItemObjectId,
                        out ItemObject resultingSlotItem))
                {
                    return;
                }
                ItemModifier resultingSlotModifier = null;
                if (!string.IsNullOrEmpty(message.ResultingSlotItemModifierId) &&
                    !objectManager.TryGetObjectWithLogging<ItemModifier>(
                        message.ResultingSlotItemModifierId,
                        out resultingSlotModifier))
                {
                    return;
                }
                resultingSlotWeapon = new MissionWeapon(
                    resultingSlotItem,
                    resultingSlotModifier,
                    message.ResultingSlotBanner,
                    message.ResultingSlotDataValue);
            }
            else
            {
                resultingSlotWeapon.Amount = message.ResultingSlotAmount;
            }

            bool hasWorldItem = TryGetWorldItem(
                message.WorldItemId,
                out SpawnedItemEntity worldItem);
            bool matchesPreviousState = hasWorldItem &&
                WorldItemMatchesPickup(
                    worldItem.WeaponCopy,
                    missionWeapon,
                    message.HasWorldItemDataValue);
            MissionWeapon resultingWorldItemWeapon = missionWeapon;
            resultingWorldItemWeapon.Amount = message.ResultingWorldItemAmount;
            bool matchesResultingState = !message.WorldItemConsumed &&
                hasWorldItem &&
                WorldItemMatchesPickup(
                    worldItem.WeaponCopy,
                    resultingWorldItemWeapon,
                    message.HasWorldItemDataValue);
            bool canApplyResultingState =
                (message.WorldItemConsumed && !hasWorldItem) ||
                matchesResultingState;
            if (!matchesPreviousState && !canApplyResultingState)
            {
                QueueNetworkPickup(message);
                Logger.Warning(
                    "Deferred weapon pickup until world item {WorldItemId} matches its canonical identity",
                    message.WorldItemId);
                return;
            }

            if (!TryGetActiveAgent(message.AgentId, out CoopAgentInfo agentInfo))
            {
                Logger.Warning(
                    "Deferred weapon pickup slot transition without active agent={AgentId}",
                    message.AgentId);
                messageBroker.Publish(
                    this,
                    new WeaponPickupApplied(
                        message.AgentId,
                        message.EquipmentIndex,
                        message.WorldItemId,
                        message.ResultingWorldItemAmount,
                        message.WorldItemConsumed,
                        slotTransitionApplied: false,
                        pickupId: message.PickupId));
                QueueNetworkPickup(message);
                RetryPendingNetworkPickupsWithActiveAgents();
                return;
            }

            if (canApplyResultingState)
            {
                ApplyResultingPickupState(
                    agentInfo,
                    message.EquipmentIndex,
                    message.CurrentEquipment,
                    ref resultingSlotWeapon);
            }
            else
            {
                ApplyWeaponPickup(
                    agentInfo,
                    worldItem,
                    message.EquipmentIndex,
                    ref missionWeapon,
                    message.CurrentEquipment,
                    message.PreviousSlotAmount,
                    message.PreviousWorldItemAmount,
                    message.ResultingSlotAmount,
                    message.ResultingWorldItemAmount,
                    message.WorldItemConsumed,
                    ref resultingSlotWeapon);
            }
            messageBroker.Publish(
                this,
                new WeaponPickupApplied(
                    message.AgentId,
                    message.EquipmentIndex,
                    message.WorldItemId,
                    message.ResultingWorldItemAmount,
                    message.WorldItemConsumed,
                    pickupId: message.PickupId));
            TrackResolvedPickup(message.PickupId);
            RetryPendingNetworkPickups(message.WorldItemId);
        }

        private void RejectPendingIdentityPickups(
            Queue<PendingIdentityPickup> pending,
            string reason)
        {
            PendingIdentityPickup[] pickups = pending.ToArray();
            for (int i = pickups.Length - 1; i >= 0; i--)
                RejectWeaponPickup(pickups[i], reason);
        }

        private void QueueNetworkPickup(NetworkWeaponPickedup message)
        {
            if (!pendingNetworkPickups.TryGetValue(
                    message.WorldItemId,
                    out Queue<PendingNetworkPickup> pending))
            {
                while (pendingNetworkPickups.Count >= MaxPendingNetworkWorldItems)
                {
                    Guid discardedId = Guid.Empty;
                    foreach (Guid candidateId in pendingNetworkPickups.Keys)
                    {
                        discardedId = candidateId;
                        break;
                    }
                    if (discardedId == Guid.Empty) break;
                    RequestWeaponStateResync(
                        discardedId,
                        pendingNetworkPickups[discardedId],
                        null);
                    pendingNetworkPickups.Remove(discardedId);
                    Logger.Warning(
                        "Discarded deferred weapon pickups for world item {WorldItemId} at pending limit",
                        discardedId);
                }

                pending = new Queue<PendingNetworkPickup>();
                pendingNetworkPickups.Add(message.WorldItemId, pending);
            }

            if (pending.Count >= MaxPendingNetworkPickupsPerWorldItem)
            {
                RequestWeaponStateResync(message.WorldItemId, pending, message);
                pending.Clear();
            }
            pending.Enqueue(new PendingNetworkPickup(message));
        }

        private void RequestWeaponStateResync(
            Guid worldItemId,
            Queue<PendingNetworkPickup> pending,
            NetworkWeaponPickedup additional)
        {
            string requesterControllerId = controllerIdProvider?.ControllerId;
            if (worldItemId == Guid.Empty || string.IsNullOrEmpty(requesterControllerId))
            {
                Logger.Warning(
                    "Unable to request weapon state resync for world item {WorldItemId}",
                    worldItemId);
                return;
            }

            var targets = new HashSet<(Guid AgentId, EquipmentIndex Slot)>();
            var requiredPickupIds = new HashSet<Guid>();
            if (pending != null)
            {
                foreach (PendingNetworkPickup pickup in pending)
                {
                    targets.Add((pickup.Message.AgentId, pickup.Message.EquipmentIndex));
                    if (pickup.Message.PickupId != Guid.Empty)
                        requiredPickupIds.Add(pickup.Message.PickupId);
                }
            }
            if (additional != null)
            {
                targets.Add((additional.AgentId, additional.EquipmentIndex));
                if (additional.PickupId != Guid.Empty)
                    requiredPickupIds.Add(additional.PickupId);
            }

            if (!latestResyncRequests.TryGetValue(
                    worldItemId,
                    out ResyncRequestState state))
            {
                while (latestResyncRequests.Count >= MaxPendingNetworkWorldItems &&
                       resyncRequestOrder.Count > 0)
                {
                    Guid expiredWorldItemId = resyncRequestOrder.Dequeue();
                    RemoveResyncRequest(expiredWorldItemId);
                }
                resyncRequestOrder.Enqueue(worldItemId);
                state = new ResyncRequestState(worldItemId, requesterControllerId);
                latestResyncRequests.Add(worldItemId, state);
            }
            state.Merge(targets, requiredPickupIds);
            state.RetryElapsedSeconds = 0f;
            SendResyncRequests(state, includeAnsweredTargets: false);
            Logger.Warning(
                "Requested weapon state resync for world item {WorldItemId} after pending limit",
                worldItemId);
        }

        private void RetryPendingNetworkPickups(Guid worldItemId)
        {
            if (!pendingNetworkPickups.TryGetValue(
                    worldItemId,
                    out Queue<PendingNetworkPickup> pending))
            {
                return;
            }

            pendingNetworkPickups.Remove(worldItemId);
            while (pending.Count > 0)
                ApplyOrQueueNetworkPickup(pending.Dequeue().Message);
        }

        private void RetryPendingNetworkPickupsWithActiveAgents()
        {
            if (pendingNetworkPickups.Count == 0) return;

            var readyWorldItemIds = new List<Guid>();
            foreach (KeyValuePair<Guid, Queue<PendingNetworkPickup>> pair in pendingNetworkPickups)
            {
                foreach (PendingNetworkPickup pickup in pair.Value)
                {
                    if (!TryGetActiveAgent(pickup.Message.AgentId, out _)) continue;

                    readyWorldItemIds.Add(pair.Key);
                    break;
                }
            }

            foreach (Guid worldItemId in readyWorldItemIds)
                RetryPendingNetworkPickups(worldItemId);
        }

        private bool TryGetActiveAgent(Guid agentId, out CoopAgentInfo agentInfo) =>
            networkAgentRegistry.TryGetAgentInfo(agentId, out agentInfo) &&
            agentInfo.Agent != null &&
            agentInfo.Agent.Mission == Mission.Current &&
            agentInfo.Agent.IsActive();

        private void HandleNetworkWeaponPickupSlotState(
            MessagePayload<NetworkWeaponPickupSlotState> payload)
        {
            NetworkWeaponPickupSlotState message = payload.What;
            GameThread.RunSafe(() => ApplyWeaponPickupSlotState(message));
        }

        private void ApplyWeaponPickupSlotState(NetworkWeaponPickupSlotState message)
        {
            if (message == null ||
                message.EquipmentIndex < EquipmentIndex.WeaponItemBeginSlot ||
                message.EquipmentIndex >= EquipmentIndex.NumAllWeaponSlots ||
                !networkAgentRegistry.TryGetAgentInfo(message.AgentId, out CoopAgentInfo agentInfo) ||
                agentInfo.Agent == null)
            {
                return;
            }

            ResyncRequestState request = null;
            if (message.RequestId != Guid.Empty &&
                (!latestResyncRequests.TryGetValue(
                        message.WorldItemId,
                        out request) ||
                 request.RequestId != message.RequestId ||
                 !request.AllTargets.Contains(
                     (message.AgentId, message.EquipmentIndex))))
            {
                return;
            }

            if (message.RequestId != Guid.Empty &&
                (string.IsNullOrEmpty(message.ResponderControllerId) ||
                 message.StateRevision < agentInfo.AuthorityRevision ||
                 (message.StateRevision == agentInfo.AuthorityRevision &&
                  message.ResponderControllerId != agentInfo.CurrentAuthority) ||
                 (appliedSlotStateRevisions.TryGetValue(
                      (message.AgentId, message.EquipmentIndex),
                      out long appliedSlotRevision) &&
                  message.StateRevision < appliedSlotRevision)))
            {
                return;
            }

            bool targetPending = request.PendingTargets.Contains(
                (message.AgentId, message.EquipmentIndex));
            if (!targetPending &&
                (!appliedSlotStateRevisions.TryGetValue(
                     (message.AgentId, message.EquipmentIndex),
                     out long answeredRevision) ||
                 message.StateRevision <= answeredRevision))
            {
                return;
            }

            MissionWeapon weapon = default;
            if (!string.IsNullOrEmpty(message.ItemObjectId))
            {
                if (!objectManager.TryGetObjectWithLogging<ItemObject>(
                        message.ItemObjectId,
                        out ItemObject item))
                {
                    return;
                }
                ItemModifier modifier = null;
                if (!string.IsNullOrEmpty(message.ItemModifierId) &&
                    !objectManager.TryGetObjectWithLogging<ItemModifier>(
                        message.ItemModifierId,
                        out modifier))
                {
                    return;
                }
                Banner banner = string.IsNullOrEmpty(message.BannerCode)
                    ? null
                    : new Banner(message.BannerCode);
                weapon = new MissionWeapon(item, modifier, banner, message.DataValue);
            }

            SupersedePendingSlotTransitions(
                message.WorldItemId,
                message.AgentId,
                message.EquipmentIndex);

            ApplyResultingPickupState(
                agentInfo,
                message.EquipmentIndex,
                message.Equipment,
                ref weapon);
            if (message.RequestId != Guid.Empty)
            {
                appliedSlotStateRevisions[(message.AgentId, message.EquipmentIndex)] =
                    message.StateRevision;
                request.PendingTargets.Remove(
                    (message.AgentId, message.EquipmentIndex));
                request.MissingTargetSeconds.Remove(
                    (message.AgentId, message.EquipmentIndex));
                request.CompletionPending = false;
                request.CompletionElapsedSeconds = 0f;
                TryCompleteResyncRequest(message.WorldItemId, request);
            }
        }

        private void HandleNetworkWeaponDropStateResponse(
            MessagePayload<NetworkWeaponDropStateResponse> payload)
        {
            NetworkWeaponDropStateResponse message = payload.What;
            GameThread.RunSafe(() => ApplyWeaponDropStateResponse(message));
        }

        private void ApplyWeaponDropStateResponse(NetworkWeaponDropStateResponse message)
        {
            if (message == null ||
                message.RequestId == Guid.Empty ||
                message.WorldItemId == Guid.Empty ||
                !latestResyncRequests.TryGetValue(
                    message.WorldItemId,
                    out ResyncRequestState request) ||
                request.RequestId != message.RequestId ||
                (appliedWorldStateRevisions.TryGetValue(
                    message.WorldItemId,
                    out long appliedRevision) &&
                 message.StateRevision < appliedRevision))
            {
                return;
            }

            var includedPickupIds = new HashSet<Guid>(
                message.IncludedPickupIds ?? Array.Empty<Guid>());
            bool acknowledgesRequiredPickup = request.RequiredPickupIds.Count == 0;
            foreach (Guid pickupId in includedPickupIds)
            {
                if (request.RequiredPickupIds.Contains(pickupId))
                {
                    acknowledgesRequiredPickup = true;
                    break;
                }
            }
            bool appliesNewSnapshot = !appliedWorldStateRevisions.TryGetValue(
                message.WorldItemId,
                out long currentRevision) ||
                message.StateRevision > currentRevision;
            if (!acknowledgesRequiredPickup && !appliesNewSnapshot) return;

            foreach (Guid pickupId in includedPickupIds)
                TrackResolvedPickup(pickupId);
            RemovePendingPickups(message.WorldItemId, includedPickupIds);
            request.RequiredPickupIds.ExceptWith(includedPickupIds);
            appliedWorldStateRevisions[message.WorldItemId] = message.StateRevision;
            bool worldResponseChanged = request.MergeWorldResponse(message);
            request.WorldStateAccepted = request.RequiredPickupIds.Count == 0 &&
                request.PendingWorldResponse != null;
            if (request.WorldStateAccepted &&
                (request.PendingWorldResponse.StateRevision > request.PublishedWorldStateRevision ||
                 worldResponseChanged))
            {
                request.PublishedWorldStateRevision = request.PendingWorldResponse.StateRevision;
                messageBroker.Publish(
                    this,
                    new AcceptedWeaponDropStateResponse(request.PendingWorldResponse));
            }
            TryCompleteResyncRequest(message.WorldItemId, request);
        }

        private void TryCompleteResyncRequest(Guid worldItemId, ResyncRequestState request)
        {
            if (request == null ||
                !request.WorldStateAccepted ||
                request.PendingTargets.Count > 0)
            {
                return;
            }
            if (!request.CompletionPending)
            {
                request.CompletionPending = true;
                request.CompletionElapsedSeconds = 0f;
                request.RetryElapsedSeconds = 0f;
            }
        }

        private void SendResyncRequests(
            ResyncRequestState state,
            bool includeAnsweredTargets)
        {
            foreach (NetworkWeaponDropResyncRequest request in
                     state.CreateRequests(includeAnsweredTargets))
            {
                messageBroker.Publish(this, request);
                network.SendAll(request);
            }
        }

        private void UpdateUnavailableResyncTargets(ResyncRequestState state, float dt)
        {
            var snapshot = new List<(Guid AgentId, EquipmentIndex Slot)>(state.PendingTargets);
            foreach ((Guid agentId, EquipmentIndex slot) in snapshot)
            {
                var target = (agentId, slot);
                if (networkAgentRegistry.TryGetAgentInfo(
                        agentId,
                        out CoopAgentInfo agentInfo) &&
                    agentInfo.Agent != null)
                {
                    state.MissingTargetSeconds.Remove(target);
                    continue;
                }

                state.MissingTargetSeconds.TryGetValue(target, out float missingSeconds);
                missingSeconds += Math.Max(0f, dt);
                if (missingSeconds < MissingResyncTargetTimeoutSeconds)
                {
                    state.MissingTargetSeconds[target] = missingSeconds;
                    continue;
                }
                state.MissingTargetSeconds.Remove(target);
                state.PendingTargets.Remove(target);
            }
        }

        private void RemoveResyncRequest(Guid worldItemId)
        {
            if (!latestResyncRequests.TryGetValue(
                    worldItemId,
                    out ResyncRequestState request))
            {
                return;
            }

            latestResyncRequests.Remove(worldItemId);
            appliedWorldStateRevisions.Remove(worldItemId);
            foreach ((Guid agentId, EquipmentIndex slot) in request.AllTargets)
                appliedSlotStateRevisions.Remove((agentId, slot));
            CompactResyncRequestOrder();
        }

        private void CompactResyncRequestOrder()
        {
            if (resyncRequestOrder.Count == 0) return;

            var retained = new Queue<Guid>();
            var seen = new HashSet<Guid>();
            while (resyncRequestOrder.Count > 0)
            {
                Guid worldItemId = resyncRequestOrder.Dequeue();
                if (seen.Add(worldItemId) && latestResyncRequests.ContainsKey(worldItemId))
                    retained.Enqueue(worldItemId);
            }
            while (retained.Count > 0)
                resyncRequestOrder.Enqueue(retained.Dequeue());
        }

        private void SupersedePendingSlotTransitions(
            Guid worldItemId,
            Guid agentId,
            EquipmentIndex equipmentIndex)
        {
            if (!pendingNetworkPickups.TryGetValue(
                    worldItemId,
                    out Queue<PendingNetworkPickup> pending))
            {
                return;
            }

            var retained = new Queue<PendingNetworkPickup>();
            while (pending.Count > 0)
            {
                PendingNetworkPickup pickup = pending.Dequeue();
                if (pickup.Message.AgentId == agentId &&
                    pickup.Message.EquipmentIndex == equipmentIndex)
                {
                    TrackResolvedPickup(pickup.Message.PickupId);
                    continue;
                }
                retained.Enqueue(pickup);
            }

            if (retained.Count == 0)
                pendingNetworkPickups.Remove(worldItemId);
            else
                pendingNetworkPickups[worldItemId] = retained;
        }

        private void RemovePendingPickups(Guid worldItemId, HashSet<Guid> pickupIds)
        {
            if (pickupIds.Count == 0 ||
                !pendingNetworkPickups.TryGetValue(
                    worldItemId,
                    out Queue<PendingNetworkPickup> pending))
            {
                return;
            }

            var retained = new Queue<PendingNetworkPickup>();
            while (pending.Count > 0)
            {
                PendingNetworkPickup pickup = pending.Dequeue();
                if (!pickupIds.Contains(pickup.Message.PickupId))
                    retained.Enqueue(pickup);
            }

            if (retained.Count == 0)
                pendingNetworkPickups.Remove(worldItemId);
            else
                pendingNetworkPickups[worldItemId] = retained;
        }

        private void TrackResolvedPickup(Guid pickupId)
        {
            if (pickupId == Guid.Empty || !resolvedPickupIds.Add(pickupId)) return;

            resolvedPickupIdOrder.Enqueue(pickupId);
            while (resolvedPickupIdOrder.Count > MaxResolvedPickupIds)
                resolvedPickupIds.Remove(resolvedPickupIdOrder.Dequeue());
        }

        private void RejectWeaponPickup(PendingIdentityPickup pending, string reason)
        {
            WeaponPickedup pickup = pending.Pickup;
            if (!networkAgentRegistry.TryGetAgentInfo(pending.AgentId, out CoopAgentInfo agentInfo) ||
                agentInfo.Agent == null ||
                pickup.EquipmentIndex < EquipmentIndex.WeaponItemBeginSlot ||
                pickup.EquipmentIndex >= EquipmentIndex.NumAllWeaponSlots)
            {
                Logger.Warning(
                    "Rejected weapon pickup without rollback agent={AgentId} reason={Reason}",
                    pending.AgentId,
                    reason);
                return;
            }

            Agent agent = agentInfo.Agent;
            MissionWeapon current = agent.Equipment[pickup.EquipmentIndex];
            if (!WeaponMatches(current, pickup.ResultingSlotWeapon) ||
                current.Amount != pickup.ResultingSlotWeapon.Amount)
            {
                Logger.Warning(
                    "Rejected stale weapon pickup without replacing newer slot state agent={AgentId} reason={Reason}",
                    pending.AgentId,
                    reason);
                return;
            }

            using (new AllowedThread())
            {
                if (!current.IsEmpty)
                    agent.RemoveEquippedWeapon(pickup.EquipmentIndex);
                if (!pickup.PreviousSlotWeapon.IsEmpty)
                {
                    MissionWeapon previous = pickup.PreviousSlotWeapon;
                    agent.EquipWeaponWithNewEntity(pickup.EquipmentIndex, ref previous);
                }
                pickup.PreviousEquipment.Apply(agent);
            }
            agentInfo.RecordAuthoritativeEquipment(pickup.PreviousEquipment);
            Logger.Warning(
                "Rejected weapon pickup without canonical identity agent={AgentId} reason={Reason}",
                pending.AgentId,
                reason);
        }

        private bool TryGetWorldItem(Guid worldItemId, out SpawnedItemEntity worldItem)
        {
            if (worldItemRegistry.TryGet(worldItemId, out worldItem))
            {
                if (IsWorldItemAvailable(worldItem))
                    return true;

                worldItemRegistry.Remove(worldItemId);
                worldItem = null;
            }

            Mission mission = Mission.Current;
            if (mission == null) return false;

            foreach (MissionObject missionObject in mission.MissionObjects)
            {
                if (!(missionObject is SpawnedItemEntity candidate) ||
                    candidate.Id.CreatedAtRuntime ||
                    !IsWorldItemAvailable(candidate))
                    continue;
                if (worldItemRegistry.GetOrCreateId(candidate) != worldItemId)
                    continue;

                worldItem = candidate;
                return true;
            }

            return false;
        }

        internal static bool IsWorldItemAvailable(SpawnedItemEntity worldItem)
        {
            return worldItem != null &&
                IsWorldItemStateAvailable(
                    worldItem.IsRemoved,
                    worldItem.IsDeactivated,
                    worldItem.GameEntity.IsValid);
        }

        internal static bool IsWorldItemStateAvailable(
            bool isRemoved,
            bool isDeactivated,
            bool isGameEntityValid)
        {
            return !isRemoved && !isDeactivated && isGameEntityValid;
        }

        internal static void ApplyWeaponPickup(
            CoopAgentInfo agentInfo,
            SpawnedItemEntity worldItem,
            EquipmentIndex equipmentIndex,
            ref MissionWeapon missionWeapon,
            AgentEquipmentData currentEquipment,
            short previousSlotAmount,
            short previousWorldItemAmount,
            short resultingSlotAmount,
            short resultingWorldItemAmount,
            bool worldItemConsumed,
            ref MissionWeapon resultingSlotWeapon)
        {
            Agent agent = agentInfo.Agent;
            agentInfo.RecordAuthoritativeEquipment(currentEquipment);
            using (new AllowedThread())
            {
                ApplyWeaponAmounts(
                    agent,
                    worldItem,
                    equipmentIndex,
                    previousSlotAmount,
                    previousWorldItemAmount);

                bool slotContainsCanonical =
                    equipmentIndex >= EquipmentIndex.WeaponItemBeginSlot &&
                    equipmentIndex < EquipmentIndex.NumAllWeaponSlots &&
                    ReferenceEquals(agent.Equipment[equipmentIndex].Item, resultingSlotWeapon.Item);
                if (worldItem == null || (!worldItemConsumed && !slotContainsCanonical))
                    ApplyDetachedWeaponPickup(agent, equipmentIndex, ref resultingSlotWeapon);
                else if (worldItemConsumed)
                    ApplyWorldItemPickup(worldItem, agent, isSuccessful: true, (int)equipmentIndex);

                ApplyWeaponAmounts(
                    agent,
                    worldItem,
                    equipmentIndex,
                    resultingSlotAmount,
                    resultingWorldItemAmount);
                ReconcileResultingSlotWeapon(agent, equipmentIndex, ref resultingSlotWeapon);

                // Replay the owner's final hand selection after vanilla applies the world-item pickup.
                currentEquipment.Apply(agent);
            }
        }

        private static void ApplyResultingPickupState(
            CoopAgentInfo agentInfo,
            EquipmentIndex equipmentIndex,
            AgentEquipmentData currentEquipment,
            ref MissionWeapon resultingSlotWeapon)
        {
            Agent agent = agentInfo.Agent;
            agentInfo.RecordAuthoritativeEquipment(currentEquipment);
            using (new AllowedThread())
            {
                ReconcileResultingSlotWeapon(agent, equipmentIndex, ref resultingSlotWeapon);
                currentEquipment.Apply(agent);
            }
        }

        private static void ApplyWeaponAmounts(
            Agent agent,
            SpawnedItemEntity worldItem,
            EquipmentIndex equipmentIndex,
            short slotAmount,
            short worldItemAmount)
        {
            if (equipmentIndex >= EquipmentIndex.WeaponItemBeginSlot &&
                equipmentIndex < EquipmentIndex.NumAllWeaponSlots &&
                !agent.Equipment[equipmentIndex].IsEmpty)
            {
                agent.SetWeaponAmountInSlot(
                    equipmentIndex,
                    slotAmount,
                    enforcePrimaryItem: true);
            }

            if (worldItem != null)
                worldItem._weapon.Amount = worldItemAmount;
        }

        private static void ApplyWorldItemPickup(
            SpawnedItemEntity worldItem,
            Agent agent,
            bool isSuccessful,
            int preferenceIndex)
        {
            worldItem.OnUseStopped(agent, isSuccessful, preferenceIndex);
        }

        private static void ApplyDetachedWeaponPickup(
            Agent agent,
            EquipmentIndex equipmentIndex,
            ref MissionWeapon missionWeapon)
        {
            if (equipmentIndex == EquipmentIndex.ExtraWeaponSlot)
                agent.EquipWeaponToExtraSlotAndWield(ref missionWeapon);
            else
                agent.EquipWeaponWithNewEntity(equipmentIndex, ref missionWeapon);
        }

        private static void ReconcileResultingSlotWeapon(
            Agent agent,
            EquipmentIndex equipmentIndex,
            ref MissionWeapon resultingSlotWeapon)
        {
            if (equipmentIndex < EquipmentIndex.WeaponItemBeginSlot ||
                equipmentIndex >= EquipmentIndex.NumAllWeaponSlots)
                return;

            MissionWeapon current = agent.Equipment[equipmentIndex];
            if (WeaponMatches(current, resultingSlotWeapon)) return;

            if (!current.IsEmpty)
                agent.RemoveEquippedWeapon(equipmentIndex);
            if (!resultingSlotWeapon.IsEmpty)
                ApplyDetachedWeaponPickup(agent, equipmentIndex, ref resultingSlotWeapon);
        }

        private static bool WeaponMatches(MissionWeapon current, MissionWeapon canonical)
        {
            if (current.IsEmpty || canonical.IsEmpty)
                return current.IsEmpty && canonical.IsEmpty;

            return ReferenceEquals(current.Item, canonical.Item) &&
                ReferenceEquals(current.ItemModifier, canonical.ItemModifier) &&
                string.Equals(current.Banner?.Serialize(), canonical.Banner?.Serialize(), StringComparison.Ordinal) &&
                current.RawDataForNetwork == canonical.RawDataForNetwork;
        }

        private static bool WorldItemMatchesPickup(
            MissionWeapon current,
            MissionWeapon canonical,
            bool compareDataValue)
        {
            if (current.IsEmpty || canonical.IsEmpty)
                return current.IsEmpty && canonical.IsEmpty;

            return ReferenceEquals(current.Item, canonical.Item) &&
                ItemModifiersMatch(current.ItemModifier, canonical.ItemModifier) &&
                string.Equals(current.Banner?.Serialize(), canonical.Banner?.Serialize(), StringComparison.Ordinal) &&
                (!compareDataValue ||
                 current.RawDataForNetwork == canonical.RawDataForNetwork);
        }

        private static bool ItemModifiersMatch(ItemModifier current, ItemModifier canonical)
        {
            if (ReferenceEquals(current, canonical)) return true;
            if (current == null || canonical == null) return false;

            return current.Armor == canonical.Armor &&
                current.ChargeDamage == canonical.ChargeDamage &&
                current.Damage == canonical.Damage &&
                current.HitPoints == canonical.HitPoints &&
                current.ItemQuality == canonical.ItemQuality &&
                current.LootDropScore == canonical.LootDropScore &&
                current.Maneuver == canonical.Maneuver &&
                current.MissileSpeed == canonical.MissileSpeed &&
                current.MountHitPoints == canonical.MountHitPoints &&
                current.MountSpeed == canonical.MountSpeed &&
                string.Equals(current.Name?.ToString(), canonical.Name?.ToString(), StringComparison.Ordinal) &&
                current.PriceMultiplier == canonical.PriceMultiplier &&
                current.ProductionDropScore == canonical.ProductionDropScore &&
                current.Speed == canonical.Speed &&
                current.StackCount == canonical.StackCount;
        }

    }
}
