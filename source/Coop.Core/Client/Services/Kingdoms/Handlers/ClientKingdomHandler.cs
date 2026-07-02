using Common.Messaging;
using Common.Network;
using Common;
using Common.Util;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Client.Services.Kingdoms.Handlers;

/// <summary>
/// Client side handler for Kingdom internal and network messages
/// </summary>
public class ClientKingdomHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IKingdomCreationSettlementTracker settlementTracker;
    private readonly IKingdomDecisionDataConverter kingdomDecisionDataConverter;
    private PendingSettlementRestore? pendingKingdomCreationSettlement;

    public ClientKingdomHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IKingdomCreationSettlementTracker settlementTracker,
        IKingdomDecisionDataConverter kingdomDecisionDataConverter)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.controllerIdProvider = controllerIdProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.settlementTracker = settlementTracker;
        this.kingdomDecisionDataConverter = kingdomDecisionDataConverter;
        messageBroker.Subscribe<NetworkAddDecision>(HandleNetworkAddDecision);
        messageBroker.Subscribe<NetworkRemoveDecision>(HandleNetworkRemoveDecision);
        messageBroker.Subscribe<NetworkChangeKingdomPolicy>(HandleNetworkChangeKingdomPolicy);
        messageBroker.Subscribe<NetworkChangeKingdomDecisionVote>(HandleNetworkChangeKingdomDecisionVote);
        messageBroker.Subscribe<NetworkKingdomDecisionResolved>(HandleNetworkKingdomDecisionResolved);
        messageBroker.Subscribe<NetworkPlayerKingdomCreated>(HandleNetworkPlayerKingdomCreated);
        messageBroker.Subscribe<KingdomDecisionVoteRequested>(HandleKingdomDecisionVoteRequested);
        messageBroker.Subscribe<KingdomCreationRequested>(HandleKingdomCreationRequested);
        messageBroker.Subscribe<DecisionAdded>(HandleLocalDecisionAdded);
    }

    private void HandleKingdomCreationRequested(MessagePayload<KingdomCreationRequested> obj)
    {
        var payload = obj.What;
        pendingKingdomCreationSettlement = CapturePendingSettlementRestore();
        string partyId = null;
        string settlementId = null;
        if (pendingKingdomCreationSettlement.HasValue)
        {
            partyId = pendingKingdomCreationSettlement.Value.PartyId;
            settlementId = pendingKingdomCreationSettlement.Value.SettlementId;
            settlementTracker.Track(partyId, settlementId);
            RestoreSettlementContext(pendingKingdomCreationSettlement.Value, notifyServer: false);
        }

        var message = new NetworkRequestCreateKingdom(
            controllerIdProvider.ControllerId,
            payload.KingdomName,
            payload.CultureId,
            partyId,
            settlementId);
        network.SendAll(message);
    }

    private void HandleNetworkPlayerKingdomCreated(MessagePayload<NetworkPlayerKingdomCreated> obj)
    {
        var payload = obj.What;
        var message = new PlayerKingdomCreated(
            payload.ControllerId,
            payload.KingdomId,
            payload.KingdomName,
            payload.ClanId,
            payload.CultureId);
        messageBroker.Publish(this, message);

        RunSettlementMutation(() =>
        {
            if (payload.ControllerId == controllerIdProvider.ControllerId)
            {
                RestorePendingSettlementAfterKingdomCreation(payload.PartyId, payload.SettlementId);
            }
            else if (TryCreatePendingSettlementRestore(payload.PartyId, payload.SettlementId, out var notificationRestore))
            {
                RestoreSettlementContext(notificationRestore, notifyServer: false);
                ClearRemoteSettlementRestore(notificationRestore);
            }
        });
    }

    private void HandleLocalDecisionAdded(MessagePayload<DecisionAdded> obj)
    {
        var payload = obj.What;
        if (!TryGetKingdomId(payload.Kingdom, out var kingdomId)) return;

        var data = kingdomDecisionDataConverter.Convert(payload.Decision);
        var message = new NetworkAddDecision(
            kingdomId,
            data,
            payload.IgnoreInfluenceCost,
            payload.RandomNumber);
        network.SendAll(message);
    }

    private bool TryGetKingdomId(Kingdom kingdom, out string kingdomId)
    {
        return objectManager.TryGetIdWithLogging(kingdom, out kingdomId);
    }

    private PendingSettlementRestore? CapturePendingSettlementRestore()
    {
        var party = ResolveCreatingPlayerParty();
        var settlement = GetPartyCurrentSettlement(party)
            ?? GetCurrentSettlement()
            ?? GetEncounterSettlement();

        if (party == null || settlement == null) return null;
        if (!TryGetPartyId(party, out var partyId)) return null;
        if (!TryGetSettlementId(settlement, out var settlementId)) return null;

        settlementTracker.TrackParty(party, partyId, settlement, settlementId);
        return new PendingSettlementRestore(partyId, settlementId);
    }

    private bool TryGetPartyId(MobileParty party, out string partyId)
    {
        return objectManager.TryGetIdWithLogging(party, out partyId);
    }

    private bool TryGetSettlementId(Settlement settlement, out string settlementId)
    {
        return objectManager.TryGetIdWithLogging(settlement, out settlementId);
    }

    private static Settlement? GetPartyCurrentSettlement(MobileParty? party)
    {
        try
        {
            return party?.CurrentSettlement;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static Settlement? GetCurrentSettlement()
    {
        try
        {
            return Settlement.CurrentSettlement;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static Settlement? GetEncounterSettlement()
    {
        try
        {
            return PlayerEncounter.EncounterSettlement;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private MobileParty? ResolveCreatingPlayerParty()
    {
        if (playerManager.TryGetPlayer(controllerIdProvider.ControllerId, out var player) &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party))
        {
            return party;
        }

        try
        {
            return MobileParty.MainParty;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private void RestorePendingSettlementAfterKingdomCreation(string partyId, string settlementId)
    {
        var pending = pendingKingdomCreationSettlement;
        pendingKingdomCreationSettlement = null;

        if (!pending.HasValue &&
            TryCreatePendingSettlementRestore(partyId, settlementId, out var notificationPending))
        {
            pending = notificationPending;
        }

        if (!pending.HasValue)
        {
            return;
        }

        RunSettlementMutation(() =>
        {
            RestoreSettlementContext(pending.Value, notifyServer: true);
            settlementTracker.Complete(pending.Value.PartyId);
        });
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

    private void RestoreSettlementContext(PendingSettlementRestore pending, bool notifyServer)
    {
        if (!objectManager.TryGetObject<MobileParty>(pending.PartyId, out var party)) return;
        if (!TryGetSettlement(pending.SettlementId, out var settlement)) return;

        settlementTracker.TrackParty(party, pending.PartyId, settlement, pending.SettlementId);

        bool restoredSettlement = party.CurrentSettlement != settlement;
        if (restoredSettlement)
        {
            EnsurePartySettlement(party, settlement);
        }

        bool needsEncounter = PlayerEncounter.Current == null;
        if (notifyServer && (restoredSettlement || needsEncounter))
        {
            messageBroker.Publish(this, new StartSettlementEncounterAttempted(party, settlement));
        }
    }

    private void ClearRemoteSettlementRestore(PendingSettlementRestore pending)
    {
        if (objectManager.TryGetObject<MobileParty>(pending.PartyId, out var party))
        {
            settlementTracker.Clear(party, pending.PartyId);
            return;
        }

        settlementTracker.Clear(null, pending.PartyId);
    }

    private bool TryGetSettlement(string settlementId, out Settlement settlement)
    {
        return objectManager.TryGetObjectWithLogging(settlementId, out settlement);
    }

    private static void EnsurePartySettlement(MobileParty party, Settlement settlement)
    {
        if (party.CurrentSettlement == settlement) return;

        RunSettlementMutation(() =>
        {
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }
        });
    }

    private static void RunSettlementMutation(Action action)
    {
        if (!GameThread.Instance.IsInitialized)
        {
            action();
            return;
        }

        GameThread.RunSafe(action, blocking: true, context: nameof(ClientKingdomHandler));
    }

    private void HandleKingdomDecisionVoteRequested(MessagePayload<KingdomDecisionVoteRequested> obj)
    {
        var payload = obj.What;
        var message = new NetworkRequestKingdomDecisionVote(controllerIdProvider.ControllerId, payload.VoteData);
        network.SendAll(message);
    }

    private void HandleNetworkKingdomDecisionResolved(MessagePayload<NetworkKingdomDecisionResolved> obj)
    {
        var payload = obj.What;
        var message = new ApplyKingdomDecisionResolved(
            payload.KingdomId,
            payload.DecisionIndex,
            payload.OutcomeIndex,
            payload.IsPlayerDecision,
            payload.OutcomeKey,
            payload.NotificationText);
        messageBroker.Publish(this, message);
    }

    private void HandleNetworkChangeKingdomDecisionVote(MessagePayload<NetworkChangeKingdomDecisionVote> obj)
    {
        var payload = obj.What;
        var message = new ApplyKingdomDecisionVote(payload.ClanId, payload.VoteData);
        messageBroker.Publish(this, message);
    }

    private void HandleNetworkChangeKingdomPolicy(MessagePayload<NetworkChangeKingdomPolicy> obj)
    {
        var payload = obj.What;
        var message = new ChangeKingdomPolicy(payload.KingdomId, payload.PolicyId, payload.IsAdd);
        messageBroker.Publish(this, message);
    }

    private void HandleNetworkRemoveDecision(MessagePayload<NetworkRemoveDecision> obj)
    {
        var payload = obj.What;
        var message = new RemoveDecision(payload.KingdomId, payload.Index);
        messageBroker.Publish(this, message);
    }

    private void HandleNetworkAddDecision(MessagePayload<NetworkAddDecision> obj)
    {
        var payload = obj.What;
        if (!ShouldApplyNetworkDecision(payload.KingdomId)) return;

        var message = new AddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost, payload.RandomNumber);
        messageBroker.Publish(this, message);
    }

    private bool ShouldApplyNetworkDecision(string kingdomId)
    {
        if (string.IsNullOrWhiteSpace(kingdomId)) return false;
        if (!objectManager.TryGetObject(kingdomId, out Kingdom kingdom)) return true;
        if (!playerManager.TryGetPlayer(controllerIdProvider.ControllerId, out var player)) return true;
        if (string.IsNullOrWhiteSpace(player.ClanId)) return false;
        if (!objectManager.TryGetObject(player.ClanId, out Clan clan)) return false;

        return clan.Kingdom == kingdom;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddDecision>(HandleNetworkAddDecision);
        messageBroker.Unsubscribe<NetworkRemoveDecision>(HandleNetworkRemoveDecision);
        messageBroker.Unsubscribe<NetworkChangeKingdomPolicy>(HandleNetworkChangeKingdomPolicy);
        messageBroker.Unsubscribe<NetworkChangeKingdomDecisionVote>(HandleNetworkChangeKingdomDecisionVote);
        messageBroker.Unsubscribe<NetworkKingdomDecisionResolved>(HandleNetworkKingdomDecisionResolved);
        messageBroker.Unsubscribe<NetworkPlayerKingdomCreated>(HandleNetworkPlayerKingdomCreated);
        messageBroker.Unsubscribe<KingdomDecisionVoteRequested>(HandleKingdomDecisionVoteRequested);
        messageBroker.Unsubscribe<KingdomCreationRequested>(HandleKingdomCreationRequested);
        messageBroker.Unsubscribe<DecisionAdded>(HandleLocalDecisionAdded);
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


