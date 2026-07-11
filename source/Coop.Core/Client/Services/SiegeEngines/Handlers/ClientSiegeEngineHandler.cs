using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEngines.Messages;
using Coop.Core.Server.Services.SiegeEngines.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines.Messages;
using GameInterface.Services.SiegeEnginesConstructionProgress.Messages;
using System;
using System.Collections.Generic;
using Serilog;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace Coop.Core.Client.Services.SiegeEngines.Handlers;

/// <summary>
/// Forwards replicated siege engine container and construction progress changes to GameInterface, and
/// sends the local player's engine build/remove orders to the server.
/// </summary>
internal class ClientSiegeEngineHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientSiegeEngineHandler>();

    private sealed class PendingSlotRequest
    {
        public bool IsDeploy;
        public string SiegeEventId;
        public string ContainerId;
        public int Side;
        public string EngineTypeId;
        public int Index;
        public bool IsRanged;
        public bool MoveToReserve;
        public string ExpectedOccupantId;
    }

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly object revisionGate = new object();
    private readonly Dictionary<string, long> slotRevisions = new Dictionary<string, long>();
    private readonly List<IMessage> pendingSlotDeltas = new List<IMessage>();
    private readonly List<PendingSlotRequest> pendingLocalRequests = new List<PendingSlotRequest>();
    private string revisionEpoch;

    public ClientSiegeEngineHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkChangeSiegeEngineDeployed>(HandleDeployed);
        messageBroker.Subscribe<NetworkChangeSiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Subscribe<NetworkChangeSiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Subscribe<NetworkChangeSiegeEngineReserveRemoved>(HandleReserveRemoved);
        messageBroker.Subscribe<NetworkChangeSiegeEngineProgress>(HandleProgress);
        messageBroker.Subscribe<NetworkChangeSiegeEngineHitpoints>(HandleHitpoints);
        messageBroker.Subscribe<NetworkAddSiegeEngineMissile>(HandleMissileAdded);
        messageBroker.Subscribe<NetworkSyncSiegeEngineSlotRevisions>(HandleSlotRevisionSnapshot);
        messageBroker.Subscribe<CampaignReady>(HandleCampaignReady);
        messageBroker.Subscribe<SiegeEngineDeployRequested>(HandleDeployRequested);
        messageBroker.Subscribe<SiegeEngineRemovalRequested>(HandleRemovalRequested);
    }

    private void HandleDeployed(MessagePayload<NetworkChangeSiegeEngineDeployed> payload)
    {
        var obj = payload.What;
        lock (revisionGate)
        {
            if (!TryAdvanceSlotRevisionLocked(obj.RevisionEpoch, obj.ContainerId, obj.IsRanged, obj.Index, obj.SlotRevision, obj)) return;
            PublishDeployed(obj);
        }
    }

    private void PublishDeployed(NetworkChangeSiegeEngineDeployed obj)
    {
        messageBroker.Publish(this, new ChangeSiegeEngineDeployed(obj.ContainerId, obj.SiegeEngineId, obj.EngineTypeId, obj.Index));
    }

    private void HandleUndeployed(MessagePayload<NetworkChangeSiegeEngineUndeployed> payload)
    {
        var obj = payload.What;
        lock (revisionGate)
        {
            if (!TryAdvanceSlotRevisionLocked(obj.RevisionEpoch, obj.ContainerId, obj.IsRanged, obj.Index, obj.SlotRevision, obj)) return;
            PublishUndeployed(obj);
        }
    }

    private void PublishUndeployed(NetworkChangeSiegeEngineUndeployed obj)
    {
        messageBroker.Publish(this, new ChangeSiegeEngineUndeployed(obj.ContainerId, obj.Index, obj.IsRanged, obj.MoveToReserve));
    }

    private void HandleSlotRevisionSnapshot(MessagePayload<NetworkSyncSiegeEngineSlotRevisions> payload)
    {
        var snapshot = payload.What;
        lock (revisionGate)
        {
            // A campaign/server handler owns one epoch. Re-entering a campaign receives a complete snapshot,
            // so replace rather than merge: revisions from a prior campaign may be higher for repeated ids.
            // Do not seed the snapshot's final counters yet: queued deltas are post-save changes and must replay
            // in wire order against the transferred save state first. Keep the gate through publication so a
            // concurrently arriving live delta cannot overtake this buffered batch.
            revisionEpoch = snapshot.RevisionEpoch;
            slotRevisions.Clear();
            var replayedSlotKeys = new HashSet<string>();
            foreach (var delta in pendingSlotDeltas)
            {
                if (!DeltaBelongsToEpoch(delta, revisionEpoch)) continue;
                if (delta is NetworkChangeSiegeEngineDeployed deployed)
                {
                    replayedSlotKeys.Add(SlotKey(deployed.ContainerId, deployed.IsRanged, deployed.Index));
                    if (TryAdvanceSlotRevisionLocked(deployed.RevisionEpoch, deployed.ContainerId, deployed.IsRanged,
                            deployed.Index, deployed.SlotRevision, deployed))
                        PublishDeployed(deployed);
                }
                else if (delta is NetworkChangeSiegeEngineUndeployed undeployed)
                {
                    replayedSlotKeys.Add(SlotKey(undeployed.ContainerId, undeployed.IsRanged, undeployed.Index));
                    if (TryAdvanceSlotRevisionLocked(undeployed.RevisionEpoch, undeployed.ContainerId, undeployed.IsRanged,
                            undeployed.Index, undeployed.SlotRevision, undeployed))
                        PublishUndeployed(undeployed);
                }
            }
            pendingSlotDeltas.Clear();

            // The snapshot is authoritative for every slot, including ones with no post-save delta. Max also
            // covers any generation whose state was already present in the transferred save.
            foreach (var slot in snapshot.Slots ?? Array.Empty<SiegeEngineSlotRevision>())
            {
                var key = SlotKey(slot.ContainerId, slot.IsRanged, slot.Index);
                if (!slotRevisions.TryGetValue(key, out var current) || slot.Revision > current)
                    slotRevisions[key] = slot.Revision;
            }

            // UI orders can be raised while the transferred save is already interactive but before the
            // server's revision snapshot reaches this handler. Send them only after replaying the queued
            // post-save deltas. A click for any replay-touched slot is stale even if the occupant returned via
            // ABA, so discard it rather than retargeting unseen state; untouched slots retain observed intent.
            foreach (var request in pendingLocalRequests)
            {
                var key = SlotKey(request.ContainerId, request.IsRanged, request.Index);
                if (replayedSlotKeys.Contains(key))
                {
                    // There was no epoch/generation available when the click was captured, so even an ABA
                    // replay (A -> B -> A) is indistinguishable from the original A. Never retarget stale UI
                    // intent to the post-save generation; the user can act again on the state now displayed.
                    Logger.Warning("Ignoring pre-snapshot siege-engine request for {Slot}: buffered deltas changed that slot before the revision snapshot",
                        key);
                    continue;
                }

                SendSlotRequestLocked(request);
            }
            pendingLocalRequests.Clear();
        }
    }

    private void HandleCampaignReady(MessagePayload<CampaignReady> payload)
    {
        lock (revisionGate)
        {
            revisionEpoch = null;
            slotRevisions.Clear();
            pendingSlotDeltas.Clear();
            pendingLocalRequests.Clear();
        }
    }

    private void HandleReserveAdded(MessagePayload<NetworkChangeSiegeEngineReserveAdded> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineReserveAdded(obj.ContainerId, obj.SiegeEngineId, obj.EngineTypeId));
    }

    private void HandleReserveRemoved(MessagePayload<NetworkChangeSiegeEngineReserveRemoved> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineReserveRemoved(obj.ContainerId, obj.SiegeEngineId));
    }

    private void HandleProgress(MessagePayload<NetworkChangeSiegeEngineProgress> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineProgress(obj.SiegeEngineId, obj.IsRedeployment, obj.Value));
    }

    private void HandleHitpoints(MessagePayload<NetworkChangeSiegeEngineHitpoints> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineHitpoints(obj.SiegeEngineId, obj.Hitpoints, obj.MaxHitPoints));
    }

    private void HandleMissileAdded(MessagePayload<NetworkAddSiegeEngineMissile> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ApplySiegeEngineMissile(obj.SiegeEventId, obj.Side, obj.ShooterEngineTypeId,
            obj.ShooterSlotIndex, obj.TargetType, obj.TargetSlotIndex, obj.TargetSiegeEngineId,
            obj.CollisionTicks, obj.FireTicks, obj.HitSuccessful));
    }

    // Runs on the game thread already — published from the production-popup container patch; only resolves an id and sends, so no GameThread.RunSafe.
    private void HandleDeployRequested(MessagePayload<SiegeEngineDeployRequested> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.SiegeEvent, out var siegeEventId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;
        if (!TryGetExpectedOccupantId(obj.ExpectedOccupant, out var expectedOccupantId)) return;

        QueueOrSendSlotRequest(new PendingSlotRequest
        {
            IsDeploy = true,
            SiegeEventId = siegeEventId,
            ContainerId = containerId,
            Side = (int)obj.Side,
            EngineTypeId = obj.EngineType.StringId,
            Index = obj.Index,
            IsRanged = obj.EngineType.IsRanged,
            ExpectedOccupantId = expectedOccupantId,
        });
    }

    private void HandleRemovalRequested(MessagePayload<SiegeEngineRemovalRequested> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.SiegeEvent, out var siegeEventId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;
        if (!TryGetExpectedOccupantId(obj.ExpectedOccupant, out var expectedOccupantId)) return;

        QueueOrSendSlotRequest(new PendingSlotRequest
        {
            SiegeEventId = siegeEventId,
            ContainerId = containerId,
            Side = (int)obj.Side,
            Index = obj.Index,
            IsRanged = obj.IsRanged,
            MoveToReserve = obj.MoveToReserve,
            ExpectedOccupantId = expectedOccupantId,
        });
    }

    // Null is a meaningful expectation (the slot must still be empty). A non-null occupant must have the
    // replicated server id; silently downgrading an unresolved occupant to null would turn a replace/remove
    // click into an operation against a different slot generation.
    private bool TryGetExpectedOccupantId(SiegeEngineConstructionProgress expectedOccupant, out string expectedOccupantId)
    {
        expectedOccupantId = null;
        return expectedOccupant == null
            || objectManager.TryGetIdWithLogging(expectedOccupant, out expectedOccupantId);
    }

    private static string SlotKey(string containerId, bool isRanged, int index)
    {
        return containerId + "|" + (isRanged ? "r" : "m") + "|" + index;
    }

    private void QueueOrSendSlotRequest(PendingSlotRequest request)
    {
        lock (revisionGate)
        {
            if (revisionEpoch == null)
            {
                // A post-save delta may be waiting ahead of the snapshot. Preserve the occupant visible when
                // the click was raised; snapshot handling discards this intent if replay touched its slot.
                pendingLocalRequests.Add(request);
                return;
            }

            SendSlotRequestLocked(request);
        }
    }

    // Caller holds revisionGate so an accepted delta cannot advance the slot between reading the expected
    // generation and placing the conditional command on the connection.
    private void SendSlotRequestLocked(PendingSlotRequest request)
    {
        var key = SlotKey(request.ContainerId, request.IsRanged, request.Index);
        long expectedRevision = slotRevisions.TryGetValue(key, out var revision) ? revision : 0L;

        if (request.IsDeploy)
        {
            network.SendAll(new NetworkRequestDeploySiegeEngine(
                request.SiegeEventId,
                request.Side,
                request.EngineTypeId,
                request.Index,
                request.ExpectedOccupantId,
                expectedRevision,
                revisionEpoch));
            return;
        }

        network.SendAll(new NetworkRequestRemoveSiegeEngine(
            request.SiegeEventId,
            request.Side,
            request.Index,
            request.IsRanged,
            request.MoveToReserve,
            request.ExpectedOccupantId,
            expectedRevision,
            revisionEpoch));
    }

    // Caller holds revisionGate through both this state transition and publication of the corresponding
    // ChangeSiegeEngine* command, preserving wire order across concurrent network delivery.
    private bool TryAdvanceSlotRevisionLocked(
        string epoch,
        string containerId,
        bool isRanged,
        int index,
        long revision,
        IMessage delta)
    {
        if (string.IsNullOrEmpty(epoch)) return false;

        // The connection queue flushes post-save deltas before the revision snapshot. Hold them in exact
        // wire order until that snapshot establishes the epoch; only then may they mutate the loaded state.
        if (revisionEpoch == null)
        {
            pendingSlotDeltas.Add(delta);
            return false;
        }

        // Only an authoritative snapshot may change epochs.
        if (!string.Equals(revisionEpoch, epoch, StringComparison.Ordinal)) return false;

        var key = SlotKey(containerId, isRanged, index);
        long current = slotRevisions.TryGetValue(key, out var existing) ? existing : 0L;
        if (revision <= current) return false;

        slotRevisions[key] = revision;
        return true;
    }

    private static bool DeltaBelongsToEpoch(IMessage delta, string epoch)
    {
        if (delta is NetworkChangeSiegeEngineDeployed deployed)
            return string.Equals(deployed.RevisionEpoch, epoch, StringComparison.Ordinal);
        if (delta is NetworkChangeSiegeEngineUndeployed undeployed)
            return string.Equals(undeployed.RevisionEpoch, epoch, StringComparison.Ordinal);
        return false;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineDeployed>(HandleDeployed);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineReserveRemoved>(HandleReserveRemoved);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineProgress>(HandleProgress);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineHitpoints>(HandleHitpoints);
        messageBroker.Unsubscribe<NetworkAddSiegeEngineMissile>(HandleMissileAdded);
        messageBroker.Unsubscribe<NetworkSyncSiegeEngineSlotRevisions>(HandleSlotRevisionSnapshot);
        messageBroker.Unsubscribe<CampaignReady>(HandleCampaignReady);
        messageBroker.Unsubscribe<SiegeEngineDeployRequested>(HandleDeployRequested);
        messageBroker.Unsubscribe<SiegeEngineRemovalRequested>(HandleRemovalRequested);
    }
}
