using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using GameInterface.Services.SiegeEvents.Validation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Client.Services.SiegeEvents.Handlers;

/// <summary>
/// Sends the local player's siege entry and exit requests to the server and runs the player-local
/// menu continuation when the approval arrives.
/// </summary>
internal class ClientSiegeEntryHandler : IHandler
{
    private static readonly Serilog.ILogger Logger = LogManager.GetLogger<ClientSiegeEntryHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISiegeEventInterface siegeEventInterface;
    private readonly ISiegeInteractionGrantStore siegeInteractionGrantStore;
    private PendingEntryRequest pendingEntryRequest;
    private bool campaignEntryCompleted;
    private bool reconnectReconciliationPending;

    public ClientSiegeEntryHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ISiegeEventInterface siegeEventInterface,
        ISiegeInteractionGrantStore siegeInteractionGrantStore)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.siegeEventInterface = siegeEventInterface;
        this.siegeInteractionGrantStore = siegeInteractionGrantStore;
        messageBroker.Subscribe<BesiegeSettlementAttempted>(HandleBesiegeAttempt);
        messageBroker.Subscribe<JoinSiegeCampAttempted>(HandleJoinAttempt);
        messageBroker.Subscribe<BreakSiegeAttempted>(HandleBreakAttempt);
        messageBroker.Subscribe<NetworkSiegeEntryResult>(HandleEntryResult);
        messageBroker.Subscribe<NetworkClearStaleBesiegerCamp>(
            HandleStaleBesiegerCampClear);
        messageBroker.Subscribe<NetworkBreakSiegeApproved>(HandleBreakApproved);
        messageBroker.Subscribe<NetworkPromptSiegeDefense>(HandleDefensePrompt);
        messageBroker.Subscribe<NetworkPromptSiegePreparation>(HandlePreparationPrompt);
        messageBroker.Subscribe<NetworkPromptSiegeEnded>(HandleSiegeEndedPrompt);
        messageBroker.Subscribe<AssaultSiegeAttempted>(HandleAssaultAttempt);
        messageBroker.Subscribe<NetworkPromptSiegeAssault>(HandleAssaultPrompt);
        messageBroker.Subscribe<NetworkSnapSiegeCampPartyPosition>(HandleCampPositionSnap);
        messageBroker.Subscribe<CampaignEntryCompleted>(HandleCampaignEntryCompleted);
    }

    private void HandleCampPositionSnap(MessagePayload<NetworkSnapSiegeCampPartyPosition> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out var party)) return;

            using (new AllowedThread())
            {
                party.Position = obj.Position;
            }
        });
    }

    private void HandlePreparationPrompt(MessagePayload<NetworkPromptSiegePreparation> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.AttackerPartyId, out var attackerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.PromptSiegePreparation(attackerParty, settlement);
        });
    }

    private void HandleSiegeEndedPrompt(MessagePayload<NetworkPromptSiegeEnded> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.PromptSiegeEnded(settlement, obj.BesiegerDefeated);
        });
    }

    private void HandleDefensePrompt(MessagePayload<NetworkPromptSiegeDefense> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.AttackerPartyId, out var attackerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            // No AllowedThread wrapper: the method scopes it per section, so the non-joinable
            // defender's settlement leave routes through the normal co-op leave flow.
            siegeEventInterface.PromptSiegeDefense(attackerParty, settlement);
        });
    }

    // Runs on the game thread already — SiegeEntryFlowPatches publishes AssaultSiegeAttempted from the assault
    // menu consequence, and this only resolves ids and sends the request, so no GameThread.RunSafe is needed.
    private void HandleAssaultAttempt(MessagePayload<AssaultSiegeAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkRequestSiegeAssault(partyId, settlementId));
    }

    private void HandleAssaultPrompt(MessagePayload<NetworkPromptSiegeAssault> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.AttackerPartyId, out var attackerParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            siegeEventInterface.PromptSiegeAssault(attackerParty, settlement);
        });
    }

    // Runs on the game thread already — SiegeEntryFlowPatches publishes the *Attempted message from the besiege menu consequence, and this only resolves ids and sends the request, so no GameThread.RunSafe is needed.
    private void HandleBesiegeAttempt(MessagePayload<BesiegeSettlementAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        siegeInteractionGrantStore.TryConsumeLocal(
            partyId,
            settlementId,
            out var interactionId);
        var request = new NetworkRequestBesiegeSettlement(
            partyId,
            settlementId,
            interactionId);
        TrackPendingEntry(
            request.PartyId,
            request.SettlementId,
            request.InteractionId,
            SiegeEntryRequestType.Besiege);
        network.SendAll(request);
    }

    // Runs on the game thread already — published from the join-siege menu consequence; only resolves ids and sends, so no GameThread.RunSafe.
    private void HandleJoinAttempt(MessagePayload<JoinSiegeCampAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        siegeInteractionGrantStore.TryConsumeLocal(
            partyId,
            settlementId,
            out var interactionId);
        var request = new NetworkRequestJoinSiegeCamp(
            partyId,
            settlementId,
            interactionId);
        TrackPendingEntry(
            request.PartyId,
            request.SettlementId,
            request.InteractionId,
            SiegeEntryRequestType.Join);
        network.SendAll(request);
    }

    // Runs on the game thread already — published from the leave-siege consequence; only resolves an id and sends, so no GameThread.RunSafe.
    private void HandleBreakAttempt(MessagePayload<BreakSiegeAttempted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;

        network.SendAll(new NetworkRequestBreakSiege(partyId));
    }

    private void HandleEntryResult(MessagePayload<NetworkSiegeEntryResult> payload)
    {
        var result = payload.What;
        GameThread.RunSafe(() => ApplyEntryResult(result));
    }

    private void HandleStaleBesiegerCampClear(
        MessagePayload<NetworkClearStaleBesiegerCamp> payload)
    {
        var partyId = payload.What.PartyId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(
                    partyId,
                    out var party))
            {
                return;
            }

            using (new AllowedThread())
            {
                siegeEventInterface.ClearStaleBesiegerCamp(party);
            }
        });
    }

    private void ApplyEntryResult(NetworkSiegeEntryResult result)
    {
        if (result.RequestType == SiegeEntryRequestType.Reconnect)
        {
            if (!objectManager.TryGetId(MobileParty.MainParty, out var mainPartyId) ||
                mainPartyId != result.PartyId)
            {
                return;
            }

            reconnectReconciliationPending = true;
            TryApplyReconnectReconciliation();
            return;
        }

        if (!TryConsumePendingEntry(result))
            return;

        Settlement canonicalSettlement = null;
        if (!string.IsNullOrEmpty(result.CanonicalSettlementId) &&
            !objectManager.TryGetObjectWithLogging<Settlement>(
                result.CanonicalSettlementId,
                out canonicalSettlement))
        {
            return;
        }

        if (result.Outcome == SiegeEntryOutcome.Rejected)
        {
            Logger.Information(
                "Server rejected {RequestType} siege entry: {Reason}",
                result.RequestType,
                result.Reason);
            siegeEventInterface.ReconcileSiegeEntry(result.Disposition, canonicalSettlement);
            return;
        }

        if (result.RequestType == SiegeEntryRequestType.Besiege)
        {
            using (new AllowedThread())
            {
                siegeEventInterface.StartLocalPlayerSiegePreparation();
            }
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Settlement>(
                result.RequestedSettlementId,
                out var requestedSettlement))
        {
            return;
        }

        using (new AllowedThread())
        {
            siegeEventInterface.StartLocalPlayerJoinedSiege(requestedSettlement);
        }
    }

    private void HandleCampaignEntryCompleted(MessagePayload<CampaignEntryCompleted> payload)
    {
        campaignEntryCompleted = true;
        TryApplyReconnectReconciliation();
    }

    private void TryApplyReconnectReconciliation()
    {
        if (!campaignEntryCompleted || !reconnectReconciliationPending)
            return;

        reconnectReconciliationPending = false;
        siegeEventInterface.ReconcileReloadedSiegeEntry();
    }

    private void TrackPendingEntry(
        string partyId,
        string settlementId,
        string interactionId,
        SiegeEntryRequestType requestType)
    {
        if (pendingEntryRequest != null)
            return;

        pendingEntryRequest = new PendingEntryRequest(
            partyId,
            settlementId,
            interactionId,
            requestType);
    }

    private bool TryConsumePendingEntry(NetworkSiegeEntryResult result)
    {
        if (pendingEntryRequest == null || !pendingEntryRequest.Matches(result))
            return false;

        pendingEntryRequest = null;
        return true;
    }

    private void HandleBreakApproved(MessagePayload<NetworkBreakSiegeApproved> payload)
    {
        if (!payload.What.Approved)
        {
            Logger.Information("Server rejected the break-siege request; staying at the current menu");
            return;
        }

        if (payload.What.BattleLeaveApplied)
            return;

        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                siegeEventInterface.FinishLocalPlayerSiegeLeave();
            }
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BesiegeSettlementAttempted>(HandleBesiegeAttempt);
        messageBroker.Unsubscribe<JoinSiegeCampAttempted>(HandleJoinAttempt);
        messageBroker.Unsubscribe<BreakSiegeAttempted>(HandleBreakAttempt);
        messageBroker.Unsubscribe<NetworkSiegeEntryResult>(HandleEntryResult);
        messageBroker.Unsubscribe<NetworkClearStaleBesiegerCamp>(
            HandleStaleBesiegerCampClear);
        messageBroker.Unsubscribe<NetworkBreakSiegeApproved>(HandleBreakApproved);
        messageBroker.Unsubscribe<NetworkPromptSiegeDefense>(HandleDefensePrompt);
        messageBroker.Unsubscribe<NetworkPromptSiegePreparation>(HandlePreparationPrompt);
        messageBroker.Unsubscribe<NetworkPromptSiegeEnded>(HandleSiegeEndedPrompt);
        messageBroker.Unsubscribe<AssaultSiegeAttempted>(HandleAssaultAttempt);
        messageBroker.Unsubscribe<NetworkPromptSiegeAssault>(HandleAssaultPrompt);
        messageBroker.Unsubscribe<NetworkSnapSiegeCampPartyPosition>(HandleCampPositionSnap);
        messageBroker.Unsubscribe<CampaignEntryCompleted>(HandleCampaignEntryCompleted);
    }

    private sealed class PendingEntryRequest
    {
        private readonly string partyId;
        private readonly string settlementId;
        private readonly string interactionId;
        private readonly SiegeEntryRequestType requestType;

        public PendingEntryRequest(
            string partyId,
            string settlementId,
            string interactionId,
            SiegeEntryRequestType requestType)
        {
            this.partyId = partyId;
            this.settlementId = settlementId;
            this.interactionId = interactionId;
            this.requestType = requestType;
        }

        public bool Matches(NetworkSiegeEntryResult result) =>
            result.PartyId == partyId &&
            result.RequestedSettlementId == settlementId &&
            result.InteractionId == interactionId &&
            result.RequestType == requestType;
    }
}
