using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Interfaces;
using TaleWorlds.CampaignSystem.Encounters;
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
    // Local attempts and all response transitions run on the game thread.
    private PendingStart pendingStart;
    private uint pendingLeavePartyId;

    public ClientSettlementExitEnterHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ISettlementInterface settlementInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.settlementInterface = settlementInterface;
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

        if (!objectManager.TryGetHandleWithLogging(payload.Party, out var partyId)) return;
        if (!objectManager.TryGetHandleWithLogging(payload.Settlement, out var settlementId)) return;

        if (pendingStart != null)
            return;

        var request = new NetworkRequestStartSettlementEncounter(partyId, settlementId);
        pendingStart = new PendingStart(
            request,
            pendingLeavePartyId == 0 ? PendingStartState.Sent : PendingStartState.Queued);

        if (pendingStart.State == PendingStartState.Sent)
            network.SendAll(request);
    }

    private void Handle(MessagePayload<EndSettlementEncounterAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetHandleWithLogging(payload.Party, out var partyId)) return;

        if (pendingLeavePartyId != 0)
            return;

        pendingLeavePartyId = partyId;

        var message = new NetworkRequestEndSettlementEncounter(partyId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkStartSettlementEncounter> obj)
    {
        var payload = obj.What;
        GameThread.RunSafe(() => HandleStartApproved(payload.PartyId, payload.SettlementId));
    }

    private void HandleStartApproved(uint partyId, uint settlementId)
    {
        if (!IsPendingStart(partyId, settlementId, PendingStartState.Sent))
            return;

        pendingStart.State = PendingStartState.Approved;
        if (pendingLeavePartyId != 0)
            return;

        pendingStart = null;
        ApplySettlementEncounter(partyId, settlementId);
    }

    private void ApplySettlementEncounter(uint partyId, uint settlementId)
    {
        if (!objectManager.TryGetObjectWithLogging(partyId, out MobileParty party)) return;
        if (!objectManager.TryGetObjectWithLogging(settlementId, out Settlement settlement)) return;

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
            if (!IsPendingStart(payload.PartyId, payload.SettlementId, PendingStartState.Sent))
                return;

            pendingStart = null;
        });
    }

    private bool IsPendingStart(uint partyId, uint settlementId, PendingStartState state) =>
        pendingStart != null &&
        pendingStart.State == state &&
        pendingStart.Request.PartyId == partyId &&
        pendingStart.Request.SettlementId == settlementId;

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

    private void HandleSuppressedLeave(uint partyId)
    {
        if (pendingLeavePartyId != partyId)
            return;

        pendingLeavePartyId = 0;
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
        ApplySettlementEncounter(start.Request.PartyId, start.Request.SettlementId);
    }

    private void HandleAppliedLeave(uint partyId)
    {
        bool resolvesPendingLeave = pendingLeavePartyId != 0;
        if (resolvesPendingLeave)
        {
            if (partyId != 0 && pendingLeavePartyId != partyId)
                return;

            pendingLeavePartyId = 0;
            pendingStart = null;
        }

        if (!IsMainParty(partyId))
            return;

        if (!resolvesPendingLeave)
            pendingStart = null;

        using (new AllowedThread())
        {
            settlementInterface.EndSettlementEncounter();
        }
    }

    private bool IsMainParty(uint partyId)
    {
        var mainParty = MobileParty.MainParty;
        objectManager.TryGetHandle(mainParty, out var mainPartyId);
        return partyId == 0 || partyId == mainPartyId;
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

            CloseStaleMainPartyEncounter(payload.PartyId);
        });
    }

    // A server-driven leave (unstuck, debug teleport) carries no leave reply, so the
    // client-requested path that closes the menu never runs. Without this the town menu
    // stays open on the old settlement and the next enter cannot open its own menu.
    private void CloseStaleMainPartyEncounter(uint partyId)
    {
        if (!IsMainParty(partyId))
            return;

        if (PlayerEncounter.Current == null || PlayerEncounter.EncounterSettlement == null)
            return;

        using (new AllowedThread())
        {
            settlementInterface.EndSettlementEncounter();
        }
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
