using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using GameInterface.Services.Villages.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.Villages.Handlers;

internal class ServerVillageHostileActionHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerVillageHostileActionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ISettlementInterface settlementInterface;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
    private readonly IRaidAiInterventionConfigInterface raidAiInterventionConfigInterface;
    private readonly ConcurrentDictionary<string, PendingForceTransferRequester> pendingForceRequesters = new ConcurrentDictionary<string, PendingForceTransferRequester>();
    // Covers the approve-to-outcome window, which includes the battle itself.
    private static readonly TimeSpan ForceRequesterTimeout = TimeSpan.FromMinutes(30);

    public ServerVillageHostileActionHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISettlementInterface settlementInterface,
        IVillageHostileActionInterface villageHostileActionInterface,
        IRaidAiInterventionConfigInterface raidAiInterventionConfigInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.settlementInterface = settlementInterface;
        this.villageHostileActionInterface = villageHostileActionInterface;
        this.raidAiInterventionConfigInterface = raidAiInterventionConfigInterface;

        messageBroker.Subscribe<NetworkRequestVillageHostileAction>(Handle_NetworkRequestVillageHostileAction);
        messageBroker.Subscribe<VillageHostileActionCooldownsChanged>(Handle_VillageHostileActionCooldownsChanged);
        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
        messageBroker.Subscribe<ForceTransferOutcomeReady>(Handle_ForceTransferOutcomeReady);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestVillageHostileAction>(Handle_NetworkRequestVillageHostileAction);
        messageBroker.Unsubscribe<VillageHostileActionCooldownsChanged>(Handle_VillageHostileActionCooldownsChanged);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
        messageBroker.Unsubscribe<ForceTransferOutcomeReady>(Handle_ForceTransferOutcomeReady);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    private void Handle_NetworkRequestVillageHostileAction(MessagePayload<NetworkRequestVillageHostileAction> payload)
    {
        if (ModInformation.IsClient) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkRequestVillageHostileAction));
            return;
        }

        var request = payload.What;
        if (!TryValidateRequester(request.ControllerId, request.MobilePartyId))
        {
            Deny(peer, VillageHostileActionDeniedReason.InvalidRequester);
            return;
        }

        GameThread.RunSafe(() => TryStartHostileAction(peer, request), blocking: true, context: nameof(Handle_NetworkRequestVillageHostileAction));
    }

    private bool TryValidateRequester(string controllerId, string mobilePartyId)
    {
        if (string.IsNullOrWhiteSpace(controllerId))
            return false;

        if (!playerManager.TryGetPlayer(controllerId, out var player))
            return false;

        return player.MobilePartyId == mobilePartyId;
    }

    private void TryStartHostileAction(NetPeer peer, NetworkRequestVillageHostileAction request)
    {
        try
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(request.MobilePartyId, out var mobileParty))
            {
                Deny(peer, VillageHostileActionDeniedReason.InvalidRequester);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<Settlement>(request.SettlementId, out var settlement))
            {
                Deny(peer, VillageHostileActionDeniedReason.NonVillageSettlement);
                return;
            }

            if (!villageHostileActionInterface.CanStartHostileAction(mobileParty, settlement, request.Action, out var reason))
            {
                Logger.Information(
                    "Denied hostile action {Action} (Party={PartyId}, Settlement={SettlementId}, Reason={Reason})",
                    request.Action,
                    request.MobilePartyId,
                    request.SettlementId,
                    reason);
                Deny(peer, reason);
                return;
            }

            if (request.Action == VillageHostileAction.Raid)
                KickOtherPlayersOutOfVillage(request.ControllerId, mobileParty, settlement);

            villageHostileActionInterface.ApplyHostileAction(mobileParty, settlement, request.Action);
            villageHostileActionInterface.ApproveMapEventStart(mobileParty.Party, settlement, request.Action);
            NoteForceTransferRequester(peer, request);

            Logger.Information(
                "Approved hostile action {Action} (Party={PartyId}, Settlement={SettlementId})",
                request.Action,
                request.MobilePartyId,
                request.SettlementId);
            network.Send(peer, new NetworkVillageHostileActionStarted(request.Action, request.MobilePartyId, request.SettlementId));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to start village hostile action");
            Deny(peer, VillageHostileActionDeniedReason.Invalid);
        }
    }

    private void NoteForceTransferRequester(NetPeer peer, NetworkRequestVillageHostileAction request)
    {
        if (request.Action != VillageHostileAction.ForceVolunteers &&
            request.Action != VillageHostileAction.ForceSupplies)
            return;

        PruneExpiredForceRequesters(DateTime.UtcNow);
        // A party fights one battle at a time: a new approval means any older
        // entry for the same party resolved without an outcome (e.g. defeat),
        // so drop it instead of holding its peer until the TTL.
        foreach (var pair in pendingForceRequesters)
        {
            if (pair.Value.PartyId == request.MobilePartyId &&
                pair.Key != GetRequesterKey(request.MobilePartyId, request.SettlementId, request.Action) &&
                pendingForceRequesters.TryRemove(pair.Key, out _))
            {
                Logger.Information(
                    "ForceTransfer superseded request dropped (Request={RequestKey}, Pending={PendingCount})",
                    pair.Key,
                    pendingForceRequesters.Count);
            }
        }
        pendingForceRequesters[GetRequesterKey(request.MobilePartyId, request.SettlementId, request.Action)] = new PendingForceTransferRequester(
            peer,
            request.ControllerId,
            request.MobilePartyId,
            request.Action,
            request.SettlementId,
            DateTime.UtcNow);
    }

    internal static string GetRequesterKey(string partyId, string settlementId, VillageHostileAction action)
    {
        return partyId + "|" + settlementId + "|" + action;
    }

    private void Handle_ForceTransferOutcomeReady(MessagePayload<ForceTransferOutcomeReady> payload)
    {
        if (ModInformation.IsClient) return;

        var pool = payload.What.Pool;
        PruneExpiredForceRequesters(DateTime.UtcNow);
        if (!pendingForceRequesters.TryRemove(GetRequesterKey(pool.PartyId, pool.SettlementId, pool.Action), out var requester) ||
            !TryValidateRequester(requester.ControllerId, pool.PartyId))
        {
            Logger.Information(
                "ForceTransfer requester gone, falling back to auto-grant (Action={Action}, Party={PartyId}, Settlement={SettlementId})",
                pool.Action,
                pool.PartyId,
                pool.SettlementId);
            GameThread.RunSafe(
                () => GrantForceTransferFallback(pool),
                blocking: true,
                context: nameof(Handle_ForceTransferOutcomeReady));
            return;
        }

        if (!villageHostileActionInterface.IsForceTransferPoolValid(pool))
        {
            villageHostileActionInterface.TryConsumeForceTransfer(pool.RequestId, pool.PartyId, out _);
            Logger.Information(
                "ForceTransfer pool empty, denying screen (Action={Action}, Party={PartyId}, Settlement={SettlementId})",
                pool.Action,
                pool.PartyId,
                pool.SettlementId);
            network.Send(requester.Peer, new NetworkDenyForceTransfer(pool.Action, pool.SettlementId, VillageHostileActionDeniedReason.Invalid));
            return;
        }

        Logger.Information(
            "ForceTransfer authorized (Action={Action}, Party={PartyId}, Settlement={SettlementId}, Request={RequestId}, Pending={PendingCount})",
            pool.Action,
            pool.PartyId,
            pool.SettlementId,
            pool.RequestId,
            pendingForceRequesters.Count);
        network.Send(requester.Peer, new NetworkAuthorizeForceTransfer(pool));
    }

    private void GrantForceTransferFallback(ForceTransferPoolData pool)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(pool.PartyId, out var attacker))
            return;

        villageHostileActionInterface.GrantForceTransferPool(attacker, pool);
    }

    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient) return;

        foreach (var pair in pendingForceRequesters)
        {
            if (pair.Value.Peer == payload.What.PlayerId && pendingForceRequesters.TryRemove(pair.Key, out _))
            {
                Logger.Information(
                    "ForceTransfer requester disconnected, dropping request (Request={RequestKey}, Pending={PendingCount})",
                    pair.Key,
                    pendingForceRequesters.Count);
            }
        }

        PruneExpiredForceRequesters(DateTime.UtcNow);
    }

    private void PruneExpiredForceRequesters(DateTime utcNow)
    {
        foreach (var pair in pendingForceRequesters)
        {
            if (utcNow - pair.Value.ApprovedAtUtc <= ForceRequesterTimeout)
                continue;

            if (pendingForceRequesters.TryRemove(pair.Key, out _))
            {
                Logger.Information(
                    "ForceTransfer request expired (Request={RequestKey}, Pending={PendingCount})",
                    pair.Key,
                    pendingForceRequesters.Count);
            }
        }
    }

    private sealed class PendingForceTransferRequester
    {
        public PendingForceTransferRequester(NetPeer peer, string controllerId, string partyId, VillageHostileAction action, string settlementId, DateTime approvedAtUtc)
        {
            Peer = peer;
            ControllerId = controllerId;
            PartyId = partyId;
            Action = action;
            SettlementId = settlementId;
            ApprovedAtUtc = approvedAtUtc;
        }

        public NetPeer Peer { get; }
        public string ControllerId { get; }
        public string PartyId { get; }
        public VillageHostileAction Action { get; }
        public string SettlementId { get; }
        public DateTime ApprovedAtUtc { get; }
    }

    private void KickOtherPlayersOutOfVillage(string requestingControllerId, MobileParty raidingParty, Settlement settlement)
    {
        foreach (var player in playerManager.Players)
        {
            if (player.ControllerId == requestingControllerId)
                continue;

            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
                continue;

            if (playerParty == raidingParty || playerParty.CurrentSettlement != settlement)
                continue;

            network.SendAll(new NetworkSettlementEncounterLeaveResult(
                player.MobilePartyId,
                SettlementEncounterLeaveOutcome.Applied));
            settlementInterface.PartyLeaveSettlement(playerParty);
        }
    }

    private void Deny(NetPeer peer, VillageHostileActionDeniedReason reason)
    {
        network.Send(peer, new NetworkVillageHostileActionDenied(reason));
    }

    private void Handle_VillageHostileActionCooldownsChanged(MessagePayload<VillageHostileActionCooldownsChanged> payload)
    {
        if (ModInformation.IsClient) return;

        network.SendAll(new NetworkVillageHostileActionCooldowns(payload.What.Cooldowns ?? Array.Empty<VillageHostileActionCooldownData>()));
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(
            () => SendJoinSnapshots(payload.What.playerId),
            blocking: true,
            context: nameof(Handle_PlayerCampaignEntered));
    }

    private void SendJoinSnapshots(NetPeer peer)
    {
        SendCooldownSnapshot(peer);
        SendRaidAiInterventionConfigSnapshot(peer);
    }

    private void SendCooldownSnapshot(NetPeer peer)
    {
        var cooldowns = villageHostileActionInterface.GetActiveCooldowns();
        if (cooldowns.Length == 0)
            return;

        network.Send(peer, new NetworkVillageHostileActionCooldowns(cooldowns));
    }

    private void SendRaidAiInterventionConfigSnapshot(NetPeer peer)
    {
        raidAiInterventionConfigInterface.SendSnapshot(peer);
    }
}
