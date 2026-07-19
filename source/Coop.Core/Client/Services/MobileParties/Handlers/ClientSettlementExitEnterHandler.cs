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
            if (pendingPartyId != null)
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

        ClearPendingEncounter();

        if (!objectManager.TryGetIdWithLogging(payload.Party, out var partyId)) return;

        // Ignore the synthetic leave caused by kingdom creation UI cleanup.
        if (settlementTracker.TryConsumeLeave(payload.Party, partyId))
        {
            return;
        }

        var message = new NetworkRequestEndSettlementEncounter(partyId);

        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkStartSettlementEncounter> obj)
    {
        var payload = obj.What;

        if (!TryCompletePendingEncounter(payload.PartyId, payload.SettlementId))
            return;

        // Client applies a replicated change: run it on the game thread inside an AllowedThread so the
        // patched action proceeds without being re-intercepted/re-broadcast.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(payload.PartyId, out MobileParty party)) return;
            if (!objectManager.TryGetObjectWithLogging(payload.SettlementId, out Settlement settlement)) return;

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
        TryCompletePendingEncounter(payload.PartyId, payload.SettlementId);
    }

    private bool TryCompletePendingEncounter(string partyId, string settlementId)
    {
        lock (pendingEncounterLock)
        {
            if (pendingPartyId != partyId || pendingSettlementId != settlementId)
                return false;

            pendingPartyId = null;
            pendingSettlementId = null;
            return true;
        }
    }

    private void ClearPendingEncounter()
    {
        lock (pendingEncounterLock)
        {
            pendingPartyId = null;
            pendingSettlementId = null;
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
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                var mainParty = MobileParty.MainParty;
                objectManager.TryGetId(mainParty, out var partyId);
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
        });
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
