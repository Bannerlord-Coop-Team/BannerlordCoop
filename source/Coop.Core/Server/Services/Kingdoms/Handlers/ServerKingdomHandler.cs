using Common.Messaging;
using Common.Network;
using Common;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.Kingdoms.Handlers;

/// <summary>
/// Handles network related data for Kingdoms
/// </summary>
public class ServerKingdomHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IKingdomCreationSettlementTracker settlementTracker;
    private readonly IKingdomDecisionDataConverter kingdomDecisionDataConverter;
    private readonly Dictionary<string, PendingSettlementRestore> pendingKingdomCreationSettlements = new();

    public ServerKingdomHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IKingdomCreationSettlementTracker settlementTracker,
        IKingdomDecisionDataConverter kingdomDecisionDataConverter)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.settlementTracker = settlementTracker;
        this.kingdomDecisionDataConverter = kingdomDecisionDataConverter;
        messageBroker.Subscribe<DecisionAdded>(HandleLocalDecisionAdded);
        messageBroker.Subscribe<DecisionRemoved>(HandleLocalDecisionRemoved);
        messageBroker.Subscribe<KingdomPolicyChanged>(HandleLocalKingdomPolicyChanged);
        messageBroker.Subscribe<NetworkRequestKingdomDecisionVote>(HandleNetworkRequestKingdomDecisionVote);
        messageBroker.Subscribe<KingdomDecisionVoteChanged>(HandleLocalKingdomDecisionVoteChanged);
        messageBroker.Subscribe<KingdomDecisionResolved>(HandleLocalKingdomDecisionResolved);
        messageBroker.Subscribe<NetworkRequestCreateKingdom>(HandleNetworkRequestCreateKingdom);
        messageBroker.Subscribe<PlayerKingdomCreated>(HandleLocalPlayerKingdomCreated);
        messageBroker.Subscribe<NetworkAddDecision>(HandleNetworkAddDecision);
    }

    private void HandleNetworkRequestCreateKingdom(MessagePayload<NetworkRequestCreateKingdom> obj)
    {
        var payload = obj.What;
        var partyId = payload.PartyId;

        if (playerManager.TryGetPlayer(payload.ControllerId, out var player))
        {
            if (string.IsNullOrWhiteSpace(partyId))
            {
                partyId = player.MobilePartyId;
            }

            if (TryCreatePendingSettlementRestore(partyId, payload.SettlementId, out var pending))
            {
                pendingKingdomCreationSettlements[payload.ControllerId] = pending;
                settlementTracker.Track(pending.PartyId, pending.SettlementId);
            }

            RunSettlementMutation(() =>
            {
                RestoreCreatingPartySettlement(partyId, payload.SettlementId);
            });
        }

        messageBroker.Publish(this, new CreateKingdom(payload.ControllerId, payload.KingdomName, payload.CultureId));
    }

    private void HandleLocalPlayerKingdomCreated(MessagePayload<PlayerKingdomCreated> obj)
    {
        var payload = obj.What;

        TryGetPlayerSettlementContext(payload.ControllerId, out var partyId, out var settlementId);

        if ((string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(settlementId)) &&
            pendingKingdomCreationSettlements.TryGetValue(payload.ControllerId, out var pending))
        {
            partyId = pending.PartyId;
            settlementId = pending.SettlementId;
        }

        RunSettlementMutation(() =>
        {
            RestoreCreatingPartySettlement(partyId, settlementId);
            settlementTracker.Complete(partyId);
            pendingKingdomCreationSettlements.Remove(payload.ControllerId);
        });

        var message = new NetworkPlayerKingdomCreated(
            payload.ControllerId,
            payload.KingdomId,
            payload.KingdomName,
            payload.ClanId,
            partyId,
            settlementId,
            payload.CultureId);
        network.SendAll(message);
    }

    private static bool TryCreatePendingSettlementRestore(
        string partyId,
        string settlementId,
        out PendingSettlementRestore pending)
    {
        pending = default;
        if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(settlementId)) return false;

        pending = new PendingSettlementRestore(partyId, settlementId);
        return true;
    }

    private void RestoreCreatingPartySettlement(string partyId, string settlementId)
    {
        if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(settlementId)) return;
        if (!objectManager.TryGetObject<MobileParty>(partyId, out var party)) return;
        if (!TryGetSettlement(settlementId, out var settlement)) return;
        settlementTracker.TrackParty(party, partyId, settlement, settlementId);
        if (party.CurrentSettlement == settlement) return;

        RunSettlementMutation(() =>
        {
            using (new AllowedThread())
            {
                try
                {
                    party.CurrentSettlement = settlement;
                }
                catch (NullReferenceException)
                {
                    party.SetCurrentSettlementDirectly(settlement);
                }
            }
        });

        network.SendAll(new NetworkPartyEnterSettlement(
            Compact(settlementId, typeof(Settlement)),
            Compact(partyId, typeof(MobileParty))));
    }

    private static void RunSettlementMutation(Action action)
    {
        if (!GameThread.Instance.IsInitialized)
        {
            action();
            return;
        }

        GameThread.RunSafe(action, blocking: true, context: nameof(ServerKingdomHandler));
    }

    private bool TryGetSettlement(string settlementId, out Settlement settlement)
    {
        return objectManager.TryGetObjectWithLogging(settlementId, out settlement);
    }

    private bool TryGetPlayerSettlementContext(
        string controllerId,
        out string partyId,
        out string settlementId)
    {
        partyId = null;
        settlementId = null;

        if (!playerManager.TryGetPlayer(controllerId, out var player)) return false;
        if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) return false;
        if (party.CurrentSettlement == null) return false;
        if (!TryGetSettlementId(party.CurrentSettlement, out settlementId)) return false;

        partyId = player.MobilePartyId;
        return true;
    }

    private bool TryGetSettlementId(Settlement settlement, out string settlementId)
    {
        return objectManager.TryGetIdWithLogging(settlement, out settlementId);
    }

    private void HandleLocalKingdomDecisionResolved(MessagePayload<KingdomDecisionResolved> obj)
    {
        var payload = obj.What;

        var message = new NetworkKingdomDecisionResolved(
            payload.KingdomId,
            payload.DecisionIndex,
            payload.OutcomeIndex,
            payload.IsPlayerDecision,
            payload.OutcomeKey,
            payload.NotificationText);
        network.SendAll(message);
    }

    private void HandleLocalKingdomDecisionVoteChanged(MessagePayload<KingdomDecisionVoteChanged> obj)
    {
        var payload = obj.What;

        var message = new NetworkChangeKingdomDecisionVote(payload.ClanId, payload.VoteData);
        network.SendAll(message);
    }

    private void HandleNetworkRequestKingdomDecisionVote(MessagePayload<NetworkRequestKingdomDecisionVote> obj)
    {
        var payload = obj.What;

        messageBroker.Publish(this, new ChangeKingdomDecisionVote(payload.ControllerId, payload.VoteData));
    }

    private void HandleNetworkAddDecision(MessagePayload<NetworkAddDecision> obj)
    {
        var payload = obj.What;

        messageBroker.Publish(
            this,
            new AddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost, payload.RandomNumber));

        var message = new NetworkAddDecision(
            payload.KingdomId,
            payload.Data,
            payload.IgnoreInfluenceCost,
            payload.RandomNumber);

        if (obj.Who is NetPeer peer)
        {
            network.SendAllBut(peer, message);
            return;
        }

        network.SendAll(message);
    }

    private void HandleLocalKingdomPolicyChanged(MessagePayload<KingdomPolicyChanged> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Kingdom, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.Policy, out var policyId)) return;

        var message = new NetworkChangeKingdomPolicy(kingdomId, policyId, payload.IsAdd);
        network.SendAll(message);
    }

    private void HandleLocalDecisionRemoved(MessagePayload<DecisionRemoved> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Kingdom, out var kingdomId)) return;

        var message = new NetworkRemoveDecision(kingdomId, payload.Index);
        network.SendAll(message);
    }

    private void HandleLocalDecisionAdded(MessagePayload<DecisionAdded> obj)
    {
        var payload = obj.What;

        if (!TryGetKingdomId(payload.Kingdom, out var kingdomId)) return;

        var data = kingdomDecisionDataConverter.Convert(payload.Decision);
        var message = new NetworkAddDecision(kingdomId, data, payload.IgnoreInfluenceCost, payload.RandomNumber);
        network.SendAll(message);
    }

    private bool TryGetKingdomId(Kingdom kingdom, out string kingdomId)
    {
        return objectManager.TryGetIdWithLogging(kingdom, out kingdomId);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DecisionAdded>(HandleLocalDecisionAdded);
        messageBroker.Unsubscribe<DecisionRemoved>(HandleLocalDecisionRemoved);
        messageBroker.Unsubscribe<KingdomPolicyChanged>(HandleLocalKingdomPolicyChanged);
        messageBroker.Unsubscribe<NetworkRequestKingdomDecisionVote>(HandleNetworkRequestKingdomDecisionVote);
        messageBroker.Unsubscribe<KingdomDecisionVoteChanged>(HandleLocalKingdomDecisionVoteChanged);
        messageBroker.Unsubscribe<KingdomDecisionResolved>(HandleLocalKingdomDecisionResolved);
        messageBroker.Unsubscribe<NetworkRequestCreateKingdom>(HandleNetworkRequestCreateKingdom);
        messageBroker.Unsubscribe<PlayerKingdomCreated>(HandleLocalPlayerKingdomCreated);
        messageBroker.Unsubscribe<NetworkAddDecision>(HandleNetworkAddDecision);
    }

    private readonly struct PendingSettlementRestore
    {
        public readonly string PartyId;
        public readonly string SettlementId;

        public PendingSettlementRestore(string partyId, string settlementId)
        {
            PartyId = partyId;
            SettlementId = settlementId;
        }
    }
}
