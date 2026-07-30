using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using static GameInterface.Services.ObjectManager.ObjectManager;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Validation;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit.
/// </summary>
public class ServerSettlementExitEnterHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISettlementInterface settlementInterface;
    private readonly IKingdomCreationSettlementTracker settlementTracker;
    private readonly IPlayerManager playerManager;
    private readonly ISiegeInteractionGrantStore siegeInteractionGrantStore;
    private readonly ISiegeEntryValidator siegeEntryValidator;
    private readonly HashSet<MobileParty> requestedPartyLeaves = new HashSet<MobileParty>();
    private readonly ILogger Logger = LogManager.GetLogger<ServerSettlementExitEnterHandler>();

    public ServerSettlementExitEnterHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ISettlementInterface settlementInterface,
        IKingdomCreationSettlementTracker settlementTracker,
        IPlayerManager playerManager,
        ISiegeInteractionGrantStore siegeInteractionGrantStore,
        ISiegeEntryValidator siegeEntryValidator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.settlementInterface = settlementInterface;
        this.settlementTracker = settlementTracker;
        this.playerManager = playerManager;
        this.siegeInteractionGrantStore = siegeInteractionGrantStore;
        this.siegeEntryValidator = siegeEntryValidator;
        messageBroker.Subscribe<NetworkRequestStartSettlementEncounter>(Handle);
        messageBroker.Subscribe<NetworkRequestEndSettlementEncounter>(Handle);

        messageBroker.Subscribe<PartyEnterSettlementAttempted>(Handle);
        messageBroker.Subscribe<PartyLeaveSettlementApplied>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestStartSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<NetworkRequestEndSettlementEncounter>(Handle);

        messageBroker.Unsubscribe<PartyEnterSettlementAttempted>(Handle);
        messageBroker.Unsubscribe<PartyLeaveSettlementApplied>(Handle);
    }

    private void Handle(MessagePayload<NetworkRequestStartSettlementEncounter> obj)
    {
        var payload = obj.What;
        var peer = (NetPeer)obj.Who;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.TryGetPlayer(peer, out var player) ||
                player.MobilePartyId != payload.PartyId)
            {
                Logger.Warning(
                    "Rejecting settlement interaction for party {PartyId} because the peer does not control it",
                    payload.PartyId);
                network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                return;
            }

            if (!objectManager.TryGetObjectWithLogging(payload.PartyId, out MobileParty mobileParty))
            {
                network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                return;
            }
            if (!objectManager.TryGetObjectWithLogging(payload.SettlementId, out Settlement settlement))
            {
                network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                return;
            }

            var validation = siegeEntryValidator.ValidateSettlementInteraction(mobileParty, settlement);
            if (!validation.IsValid)
            {
                Logger.Warning(
                    "Rejecting settlement interaction for party {PartyId} at {SettlementId}: {Reason}",
                    payload.PartyId,
                    payload.SettlementId,
                    validation.Reason);
                network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                return;
            }

            if (mobileParty.CurrentSettlement != null)
            {
                if (mobileParty.CurrentSettlement == settlement)
                {
                    siegeInteractionGrantStore.Grant(
                        peer,
                        payload.InteractionId,
                        payload.PartyId,
                        payload.SettlementId,
                        settlement.SiegeEvent?.BesiegerCamp);
                    network.Send(peer, new NetworkStartSettlementEncounter(payload));
                }
                else
                {
                    Logger.Warning(
                        "Rejecting settlement entry for party {PartyId} because it is already in settlement {SettlementId}",
                        payload.PartyId,
                        objectManager.TryGetId(mobileParty.CurrentSettlement, out var currentSettlementId)
                            ? currentSettlementId
                            : mobileParty.CurrentSettlement.StringId);
                    network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                }
                return;
            }

            siegeInteractionGrantStore.Grant(
                peer,
                payload.InteractionId,
                payload.PartyId,
                payload.SettlementId,
                settlement.SiegeEvent?.BesiegerCamp);
            network.Send(peer, new NetworkStartSettlementEncounter(payload));

            // Vanilla starts under-siege and under-raid encounters outside the settlement.
            if (settlement.IsUnderSiege || (settlement.IsVillage && settlement.IsUnderRaid)) return;

            network.SendAllBut(peer, new NetworkPartyEnterSettlement(
                Compact(payload.SettlementId, typeof(Settlement)),
                Compact(payload.PartyId, typeof(MobileParty))));

            settlementInterface.PartyEnterSettlement(mobileParty, settlement);
        }, context: nameof(NetworkRequestStartSettlementEncounter));
    }

    private void Handle(MessagePayload<NetworkRequestEndSettlementEncounter> obj)
    {
        var payload = obj.What;

        GameThread.RunSafe(() =>
        {
            var peer = obj.Who as NetPeer;
            objectManager.TryGetObject<MobileParty>(payload.PartyId, out var mobileParty);
            if (settlementTracker.TryConsumeLeave(mobileParty, payload.PartyId))
            {
                if (peer != null)
                {
                    network.Send(
                        peer,
                        new NetworkSettlementEncounterLeaveResult(
                            payload.PartyId,
                            SettlementEncounterLeaveOutcome.Suppressed));
                }
                return;
            }

            if (peer == null) return;
            if (mobileParty == null)
            {
                objectManager.TryGetObjectWithLogging<MobileParty>(
                    payload.PartyId,
                    out _);
                siegeInteractionGrantStore.Revoke(peer);
                network.Send(
                    peer,
                    new NetworkSettlementEncounterLeaveResult(
                        payload.PartyId,
                        SettlementEncounterLeaveOutcome.Applied));
                return;
            }

            Exception leaveException = null;
            requestedPartyLeaves.Add(mobileParty);
            try
            {
                settlementInterface.PartyLeaveSettlement(mobileParty);
            }
            catch (Exception exception)
            {
                leaveException = exception;
            }
            finally
            {
                requestedPartyLeaves.Remove(mobileParty);
            }

            if (mobileParty.CurrentSettlement != null)
            {
                network.Send(
                    peer,
                    new NetworkSettlementEncounterLeaveResult(
                        payload.PartyId,
                        SettlementEncounterLeaveOutcome.Suppressed));
                Rethrow(leaveException);
                return;
            }

            // The sending client is currently in a settlement encounter, this is handled
            // slightly differently from ai or other clients parties
            siegeInteractionGrantStore.Revoke(peer);
            network.Send(
                peer,
                new NetworkSettlementEncounterLeaveResult(
                    payload.PartyId,
                    SettlementEncounterLeaveOutcome.Applied));

            network.SendAllBut(peer, new NetworkPartyLeaveSettlement(
                Compact(payload.PartyId, typeof(MobileParty))));
            settlementInterface.OnPartyLeftSettlement(mobileParty);
            Rethrow(leaveException);
        }, context: nameof(NetworkRequestEndSettlementEncounter));
    }

    private static void Rethrow(Exception exception)
    {
        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private void Handle(MessagePayload<PartyEnterSettlementAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Settlement, out var settlementId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.MobileParty, out var mobilePartyId)) return;

        settlementId = Compact(settlementId, typeof(Settlement));
        mobilePartyId = Compact(mobilePartyId, typeof(MobileParty));

        network.SendAll(new NetworkPartyEnterSettlement(settlementId, mobilePartyId));

        settlementInterface.OnPartyEnteredSettlement(payload.Settlement, payload.MobileParty);
    }

    private void Handle(MessagePayload<PartyLeaveSettlementApplied> obj)
    {
        var payload = obj.What;

        if (requestedPartyLeaves.Contains(payload.MobileParty)) return;
        if (!objectManager.TryGetIdWithLogging(payload.MobileParty, out var mobilePartyId)) return;

        if (settlementTracker.TryConsumeLeave(payload.MobileParty, mobilePartyId))
        {
            return;
        }

        siegeInteractionGrantStore.RevokeParty(mobilePartyId);
        network.SendAll(new NetworkPartyLeaveSettlement(
            Compact(mobilePartyId, typeof(MobileParty))));

        settlementInterface.OnPartyLeftSettlement(payload.MobileParty);
    }
}
