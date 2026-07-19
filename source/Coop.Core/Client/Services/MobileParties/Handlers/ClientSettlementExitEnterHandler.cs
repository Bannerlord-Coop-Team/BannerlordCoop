using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Kingdoms;
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
    private readonly IKingdomCreationSettlementTracker settlementTracker;

    private readonly object pendingEncounterLock = new object();
    private string pendingPartyId;
    private string pendingSettlementId;
    private bool pendingEnterApproved;
    private string pendingLeavePartyId;

    public ClientSettlementExitEnterHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ISettlementInterface settlementInterface,
        IKingdomCreationSettlementTracker settlementTracker)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.settlementInterface = settlementInterface;
        this.settlementTracker = settlementTracker;
        messageBroker.Subscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<EndSettlementEncounterAttempted>(Handle);
        messageBroker.Subscribe<NetworkEndSettlementEncounter>(Handle);
        messageBroker.Subscribe<NetworkSettlementEncounterLeaveSuppressed>(Handle);
        messageBroker.Subscribe<NetworkStartSettlementEncounter>(Handle);
        messageBroker.Subscribe<NetworkSettlementEncounterRejected>(Handle);

        messageBroker.Subscribe<NetworkPartyEnterSettlement>(Handle);
        messageBroker.Subscribe<NetworkPartyLeaveSettlement>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<StartSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<EndSettlementEncounterAttempted>(Handle);
        messageBroker.Unsubscribe<NetworkEndSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<NetworkSettlementEncounterLeaveSuppressed>(Handle);
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

        lock (pendingEncounterLock)
        {
            if (pendingPartyId != null || pendingLeavePartyId != null)
                return;

            pendingPartyId = partyId;
            pendingSettlementId = settlementId;
        }

        var message = new NetworkRequestStartSettlementEncounter(partyId, settlementId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<EndSettlementEncounterAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Party, out var partyId)) return;

        // Ignore the synthetic leave caused by kingdom creation UI cleanup.
        if (settlementTracker.TryConsumeLeave(payload.Party, partyId))
        {
            return;
        }

        lock (pendingEncounterLock)
        {
            if (pendingLeavePartyId != null)
                return;

            pendingLeavePartyId = partyId;
        }

        var message = new NetworkRequestEndSettlementEncounter(partyId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkStartSettlementEncounter> obj)
    {
        var payload = obj.What;

        if (!TryAcceptPendingEncounter(payload.PartyId, payload.SettlementId))
            return;

        ApplySettlementEncounter(payload.PartyId, payload.SettlementId);
    }

    private void ApplySettlementEncounter(string partyId, string settlementId)
    {
        // Client applies a replicated change: run it on the game thread inside an AllowedThread so the
        // patched action proceeds without being re-intercepted/re-broadcast.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(partyId, out MobileParty party)) return;
            if (!objectManager.TryGetObjectWithLogging(settlementId, out Settlement settlement)) return;

            using (new AllowedThread())
            {
                settlementInterface.StartSettlementEncounter(party, settlement);

                if (ShouldShowRaidOccupiedMenu(party, settlement))
                    GameMenu.SwitchToMenu("raid_occupied");
            }
        });
    }

    private void Handle(MessagePayload<NetworkSettlementEncounterRejected> obj)
    {
        var payload = obj.What;
        lock (pendingEncounterLock)
        {
            if (pendingPartyId != payload.PartyId || pendingSettlementId != payload.SettlementId)
                return;

            ClearPendingEncounterNoLock();
        }
    }

    private bool TryAcceptPendingEncounter(string partyId, string settlementId)
    {
        lock (pendingEncounterLock)
        {
            if (pendingPartyId != partyId || pendingSettlementId != settlementId)
                return false;

            if (pendingLeavePartyId != null)
            {
                pendingEnterApproved = true;
                return false;
            }

            ClearPendingEncounterNoLock();
            return true;
        }
    }

    private void ClearPendingEncounterNoLock()
    {
        pendingPartyId = null;
        pendingSettlementId = null;
        pendingEnterApproved = false;
    }

    private void CompletePendingLeave(string partyId)
    {
        lock (pendingEncounterLock)
        {
            if (!string.IsNullOrEmpty(partyId) && pendingLeavePartyId != partyId)
                return;

            pendingLeavePartyId = null;
            ClearPendingEncounterNoLock();
        }
    }

    private void DiscardPendingEncounterForLeave(string partyId)
    {
        lock (pendingEncounterLock)
        {
            if (!string.IsNullOrEmpty(partyId) && pendingLeavePartyId != partyId)
                return;

            ClearPendingEncounterNoLock();
        }
    }

    private static bool ShouldShowRaidOccupiedMenu(MobileParty party, Settlement settlement)
    {
        if (party?.Party?.MapEvent != null)
            return false;

        return settlement?.Party?.MapEvent?.IsActiveSlowVillageRaid() == true;
    }

    private void Handle(MessagePayload<NetworkEndSettlementEncounter> obj)
    {
        DiscardPendingEncounterForLeave(obj.What.PartyId);

        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                var mainParty = MobileParty.MainParty;
                objectManager.TryGetId(mainParty, out var partyId);

                try
                {
                    if (!string.IsNullOrEmpty(obj.What.PartyId) && obj.What.PartyId != partyId)
                    {
                        return;
                    }

                    if (settlementTracker.TryConsumeLeave(mainParty, partyId))
                    {
                        return;
                    }

                    settlementInterface.EndSettlementEncounter();
                }
                finally
                {
                    CompletePendingLeave(
                        string.IsNullOrEmpty(obj.What.PartyId) ? partyId : obj.What.PartyId);
                }
            }
        });
    }

    private void Handle(MessagePayload<NetworkSettlementEncounterLeaveSuppressed> obj)
    {
        string partyId = null;
        string settlementId = null;

        lock (pendingEncounterLock)
        {
            if (pendingLeavePartyId != obj.What.PartyId)
                return;

            pendingLeavePartyId = null;
            if (!pendingEnterApproved)
                return;

            partyId = pendingPartyId;
            settlementId = pendingSettlementId;
            ClearPendingEncounterNoLock();
        }

        ApplySettlementEncounter(partyId, settlementId);
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
        });
    }
}
