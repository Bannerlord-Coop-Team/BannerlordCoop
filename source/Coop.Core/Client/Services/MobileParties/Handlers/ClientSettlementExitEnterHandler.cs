using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Interfaces;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit on the client side.
/// </summary>
public class ClientSettlementExitEnterHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISettlementInterface settlementInterface;
    private readonly ISiegeInteractionGrantStore siegeInteractionGrantStore;
    // Local attempts and all response transitions run on the game thread.
    private PendingStart pendingStart;
    private string pendingLeavePartyId;

    public ClientSettlementExitEnterHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ISettlementInterface settlementInterface,
        ISiegeInteractionGrantStore siegeInteractionGrantStore)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.settlementInterface = settlementInterface;
        this.siegeInteractionGrantStore = siegeInteractionGrantStore;
        messageBroker.Subscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<EndSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<NetworkSettlementEncounterLeaveResult>(Handle);
        messageBroker.Subscribe<NetworkStartSettlementEncounter>(Handle);
        messageBroker.Subscribe<NetworkSettlementEncounterRejected>(Handle);

        messageBroker.Subscribe<NetworkPartyEnterSettlement>(Handle);
        messageBroker.Subscribe<NetworkPartyLeaveSettlement>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<EndSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<NetworkSettlementEncounterLeaveResult>(Handle);
        messageBroker.Unsubscribe<NetworkStartSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<NetworkSettlementEncounterRejected>(Handle);

        messageBroker.Unsubscribe<NetworkPartyEnterSettlement>(Handle);
        messageBroker.Unsubscribe<NetworkPartyLeaveSettlement>(Handle);
    }

    private void Handle(MessagePayload<StartSettlementEncounterAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Party, out var partyId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.Settlement, out var settlementId)) return;

        if (pendingStart != null)
            return;

        siegeInteractionGrantStore.ClearLocal(partyId);
        var request = new NetworkRequestStartSettlementEncounter(
            partyId,
            settlementId,
            siegeInteractionGrantStore.CreateInteractionId());
        pendingStart = new PendingStart(
            request,
            pendingLeavePartyId == null ? PendingStartState.Sent : PendingStartState.Queued);

        if (pendingStart.State == PendingStartState.Sent)
            network.SendAll(request);
    }

    private void Handle(MessagePayload<EndSettlementEncounterAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Party, out var partyId)) return;

        if (pendingLeavePartyId != null)
            return;

        pendingLeavePartyId = partyId;

        var message = new NetworkRequestEndSettlementEncounter(partyId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkStartSettlementEncounter> obj)
    {
        var payload = obj.What;
        GameThread.RunSafe(() => HandleStartApproved(
            payload.PartyId,
            payload.SettlementId,
            payload.InteractionId));
    }

    private void HandleStartApproved(
        string partyId,
        string settlementId,
        string interactionId)
    {
        if (!IsPendingStart(partyId, settlementId, interactionId, PendingStartState.Sent))
            return;

        pendingStart.State = PendingStartState.Approved;
        if (pendingLeavePartyId != null)
            return;

        var start = pendingStart;
        pendingStart = null;
        ApplySettlementEncounter(start.Request);
    }

    private void ApplySettlementEncounter(NetworkRequestStartSettlementEncounter request)
    {
        if (!objectManager.TryGetObjectWithLogging(request.PartyId, out MobileParty party)) return;
        if (!objectManager.TryGetObjectWithLogging(request.SettlementId, out Settlement settlement)) return;

        siegeInteractionGrantStore.RecordLocal(
            request.InteractionId,
            request.PartyId,
            request.SettlementId);
        using (new AllowedThread())
        {
            settlementInterface.StartSettlementEncounter(party, settlement);

            if (ShouldShowRaidOccupiedMenu(party, settlement))
                GameMenu.SwitchToMenu("raid_occupied");
        }
    }

    private void Handle(MessagePayload<NetworkSettlementEncounterRejected> obj)
    {
        var payload = obj.What;
        GameThread.RunSafe(() =>
        {
            if (!IsPendingStart(
                    payload.PartyId,
                    payload.SettlementId,
                    payload.InteractionId,
                    PendingStartState.Sent))
                return;

            pendingStart = null;
        });
    }

    private bool IsPendingStart(
        string partyId,
        string settlementId,
        string interactionId,
        PendingStartState state) =>
        pendingStart != null &&
        pendingStart.State == state &&
        pendingStart.Request.PartyId == partyId &&
        pendingStart.Request.SettlementId == settlementId &&
        pendingStart.Request.InteractionId == interactionId;

    private static bool ShouldShowRaidOccupiedMenu(MobileParty party, Settlement settlement)
    {
        if (party?.Party?.MapEvent != null)
            return false;

        return settlement?.Party?.MapEvent?.IsActiveSlowVillageRaid() == true;
    }

    private void Handle(MessagePayload<NetworkSettlementEncounterLeaveResult> obj)
    {
        var payload = obj.What;
        GameThread.RunSafe(() => HandleLeaveResult(payload));
    }

    private void HandleLeaveResult(NetworkSettlementEncounterLeaveResult result)
    {
        if (result.Outcome == SettlementEncounterLeaveOutcome.Suppressed)
        {
            HandleSuppressedLeave(result.PartyId);
            return;
        }

        HandleAppliedLeave(result.PartyId);
    }

    private void HandleSuppressedLeave(string partyId)
    {
        if (pendingLeavePartyId != partyId)
            return;

        pendingLeavePartyId = null;
        if (pendingStart == null || pendingStart.State == PendingStartState.Sent)
            return;

        var start = pendingStart;
        if (start.State == PendingStartState.Queued)
        {
            start.State = PendingStartState.Sent;
            network.SendAll(start.Request);
            return;
        }

        pendingStart = null;
        ApplySettlementEncounter(start.Request);
    }

    private void HandleAppliedLeave(string partyId)
    {
        bool resolvesPendingLeave = pendingLeavePartyId != null;
        if (resolvesPendingLeave)
        {
            if (!string.IsNullOrEmpty(partyId) && pendingLeavePartyId != partyId)
                return;

            pendingLeavePartyId = null;
            pendingStart = null;
        }

        bool isMainParty = IsMainParty(partyId);
        if (isMainParty)
        {
            using (new AllowedThread())
            {
                settlementInterface.EndSettlementEncounter();
            }
        }

        siegeInteractionGrantStore.ClearLocal(partyId);
        if (!resolvesPendingLeave && isMainParty)
        {
            pendingStart = null;
        }
    }

    private bool IsMainParty(string partyId)
    {
        var mainParty = MobileParty.MainParty;
        objectManager.TryGetId(mainParty, out var mainPartyId);
        return string.IsNullOrEmpty(partyId) || partyId == mainPartyId;
    }

    private void Handle(MessagePayload<NetworkPartyEnterSettlement> obj)
    {
        var payload = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(payload.PartyId, out MobileParty party)) return;
            if (!objectManager.TryGetObjectWithLogging(payload.SettlementId, out Settlement settlement)) return;

            using (new AllowedThread())
            {
                settlementInterface.PartyEnterSettlement(party, settlement);
            }
        });
    }

    private void Handle(MessagePayload<NetworkPartyLeaveSettlement> obj)
    {
        var payload = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(payload.PartyId, out MobileParty party)) return;

            using (new AllowedThread())
            {
                settlementInterface.PartyLeaveSettlement(party);
            }

            siegeInteractionGrantStore.ClearLocal(payload.PartyId);
        });
    }

    private enum PendingStartState
    {
        Queued,
        Sent,
        Approved,
    }

    private sealed class PendingStart
    {
        public readonly NetworkRequestStartSettlementEncounter Request;
        public PendingStartState State;

        public PendingStart(NetworkRequestStartSettlementEncounter request, PendingStartState state)
        {
            Request = request;
            State = state;
        }
    }
}
