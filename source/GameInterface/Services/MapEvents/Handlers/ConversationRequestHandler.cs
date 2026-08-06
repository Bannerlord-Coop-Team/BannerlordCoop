using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Bridges the client's <c>PlayerEncounter.RestartPlayerEncounter</c> gate to the server.
/// </summary>
/// <remarks>
/// Client: turns <see cref="ConversationRequested"/> into a <see cref="NetworkRequestConversation"/>, rate-limited to
/// at most one request every <see cref="RequestCooldown"/> so a repeatedly-retried restart does not spam the server.
/// Server: validates the request and replies with <see cref="NetworkAllowConversation"/>, or rejects it silently when
/// both parties are players or either party is already in a <see cref="TaleWorlds.CampaignSystem.MapEvents.MapEvent"/>.
/// Client (on approval): re-runs <c>PlayerEncounter.RestartPlayerEncounter</c> with the same parameters under an
/// <see cref="AllowedThread"/> so the now-approved original executes.
/// Server (additionally): while a conversation is open, the AI party is held in place for exactly one player. A
/// second player is refused the hold rather than sharing it, hostile or not. Simultaneous attackers still converge
/// on one MapEvent, but by retrying rather than by sharing: once the holder has started the battle, the map-event
/// branch approves the contender's next request so it joins that MapEvent.
/// </remarks>
internal class ConversationRequestHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ConversationRequestHandler>();

    private static readonly TimeSpan RequestCooldown = TimeSpan.FromMilliseconds(500);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly IConversationRestartContextTracker restartContextTracker;
    private readonly PlayerPartyInteractionHandler playerPartyInteractionHandler;
    private readonly IPlayerManager playerManager;
    private readonly object pvpInteractionSync = new object();

    // Client request state is game-thread-only; patch publishers run there and receive handlers use GameThread.RunSafe.
    private DateTime lastRequestSentUtc = DateTime.MinValue;
    private string pendingConversationRequestId;
    private string activeConversationRequestId;
    private bool hasActiveConversationRequest;
    private long conversationActivationVersion;
    private int approvedRestartDepth;

    // [Server] Player-vs-player interactions in progress, keyed by the attacking player's peer -> the defending
    // player's party id. Lets the defender be told when the interaction ends (the attacker has no AI party to hold,
    // so this is the only record of a PvP engagement). The defender's "hold on" popup is driven from these broadcasts.
    private readonly ConcurrentDictionary<NetPeer, PvpInteraction> pvpDefenderByAttacker =
        new ConcurrentDictionary<NetPeer, PvpInteraction>();
    private readonly Queue<IMessage> pvpInteractionNotifications = new Queue<IMessage>();
    private bool isDrainingPvpInteractionNotifications;

    private readonly struct PvpInteraction
    {
        public readonly string DefenderPartyId;
        public readonly string RequestId;

        public PvpInteraction(string defenderPartyId, string requestId)
        {
            DefenderPartyId = defenderPartyId;
            RequestId = requestId;
        }
    }

    public ConversationRequestHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ConversationPartyTracker conversationPartyTracker,
        IConversationRestartContextTracker restartContextTracker,
        PlayerPartyInteractionHandler playerPartyInteractionHandler,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.conversationPartyTracker = conversationPartyTracker;
        this.restartContextTracker = restartContextTracker;
        this.playerPartyInteractionHandler = playerPartyInteractionHandler;
        this.playerManager = playerManager;

        messageBroker.Subscribe<ConversationRequested>(Handle_ConversationRequested);
        messageBroker.Subscribe<NetworkRequestConversation>(Handle_NetworkRequestConversation);
        messageBroker.Subscribe<NetworkAllowConversation>(Handle_NetworkAllowConversation);
        messageBroker.Subscribe<ConversationEnded>(Handle_ConversationEnded);
        messageBroker.Subscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Subscribe<NetworkConversationDenied>(Handle_NetworkConversationDenied);
        messageBroker.Subscribe<NetworkPvpDefenderShown>(Handle_NetworkPvpDefenderShown);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ConversationRequested>(Handle_ConversationRequested);
        messageBroker.Unsubscribe<NetworkRequestConversation>(Handle_NetworkRequestConversation);
        messageBroker.Unsubscribe<NetworkAllowConversation>(Handle_NetworkAllowConversation);
        messageBroker.Unsubscribe<ConversationEnded>(Handle_ConversationEnded);
        messageBroker.Unsubscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Unsubscribe<NetworkConversationDenied>(Handle_NetworkConversationDenied);
        messageBroker.Unsubscribe<NetworkPvpDefenderShown>(Handle_NetworkPvpDefenderShown);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    /// <summary>Route a local encounter request to the server-side approval flow.</summary>
    private void Handle_ConversationRequested(MessagePayload<ConversationRequested> payload)
    {
        var request = payload.What;

        if (ModInformation.IsServer)
        {
            ProcessServerConversationRequest(request);
            return;
        }

        if (PlayerPartyInteractionDialogState.HasActiveState)
            return;

        var now = DateTime.UtcNow;
        if (now - lastRequestSentUtc < RequestCooldown)
            return; // drop: at most one request per cooldown window

        if (!objectManager.TryGetIdWithLogging(request.DefenderParty, out var defenderId)) return;
        if (!objectManager.TryGetIdWithLogging(request.AttackerParty, out var attackerId)) return;

        lastRequestSentUtc = now;
        var requestId = restartContextTracker.Capture(PlayerEncounter.Current);
        pendingConversationRequestId = requestId;

        Logger.Debug("Requesting conversation from server. AttackerId={AttackerId}, DefenderId={DefenderId}", attackerId, defenderId);

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestConversation(
            defenderId,
            attackerId,
            request.ForcePlayerOutFromSettlement,
            request.Source,
            request.ArmyTalkEncounter,
            requestId));
    }

    private void ProcessServerConversationRequest(ConversationRequested request)
    {
        var attackerIsPlayer = request.AttackerParty?.MobileParty?.IsPlayerParty() == true;
        var defenderIsPlayer = request.DefenderParty?.MobileParty?.IsPlayerParty() == true;
        if (attackerIsPlayer == defenderIsPlayer) return;

        var playerParty = attackerIsPlayer
            ? request.AttackerParty.MobileParty
            : request.DefenderParty.MobileParty;

        if (!PlayerManager.TryGetControlledObjectInfo(playerParty, out var controlInfo)) return;
        if (!playerManager.TryGetPeer(controlInfo.ObjectControllerId, out var playerPeer)) return;
        if (!objectManager.TryGetIdWithLogging(request.DefenderParty, out var defenderId)) return;
        if (!objectManager.TryGetIdWithLogging(request.AttackerParty, out var attackerId)) return;

        Logger.Debug(
            "Starting server-detected conversation. AttackerId={AttackerId}, DefenderId={DefenderId}",
            attackerId,
            defenderId);

        ProcessConversationRequest(
            playerPeer,
            new NetworkRequestConversation(
                defenderId,
                attackerId,
                request.ForcePlayerOutFromSettlement,
                request.Source,
                false,
                requestId: null),
            serverDetected: true);
    }

    /// <summary>[Server] Validate the request; reply to allow, or stay silent to reject.</summary>
    private void Handle_NetworkRequestConversation(MessagePayload<NetworkRequestConversation> payload)
    {
        if (ModInformation.IsClient) return;

        var request = payload.What;

        if (!(payload.Who is NetPeer requestingPeer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkRequestConversation));
            return;
        }

        GameThread.RunSafe(
            () => ProcessConversationRequest(requestingPeer, request, serverDetected: false),
            context: nameof(Handle_NetworkRequestConversation));
    }

    private void ProcessConversationRequest(
        NetPeer requestingPeer,
        NetworkRequestConversation request,
        bool serverDetected)
    {
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(request.AttackerId, out var attacker)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(request.DefenderId, out var defender)) return;

        if (!TryAcceptConversationRequest(requestingPeer, request, attacker, defender, out var aiParty, out var aiPartyId, out var playerPartyId, out var isPlayerVsPlayer))
            return;

        if (aiParty == null)
        {
            // No AI mobile party involved (a settlement side, or both sides are players); nothing to hold.
            SendAllowConversation(requestingPeer, request);

            // PvP: tell the defending player's client to show a "hold on" popup while the attacker drives the
            // interaction. request.DefenderId is the engaged (non-initiating) party.
            if (isPlayerVsPlayer)
                NotifyPvpInteractionStarted(requestingPeer, request, attacker);

            return;
        }

        HoldAndApprove(requestingPeer, request, aiPartyId, playerPartyId, serverDetected);
    }

    /// <summary>
    /// [Server] Identifies the AI side and rejects when both parties are already in
    /// (separate) battles. Returns false (rejection logged, and the requester told when the party is engaged by
    /// another player) when the request must not proceed. On success <paramref name="aiParty"/> is the AI mobile
    /// party to hold, or null when only a settlement side is involved or both sides are players — in which case
    /// <paramref name="isPlayerVsPlayer"/> is set so the caller can drive the defender's "hold on" popup.
    /// </summary>
    private bool TryAcceptConversationRequest(
        NetPeer requestingPeer,
        NetworkRequestConversation request,
        PartyBase attacker,
        PartyBase defender,
        out MobileParty aiParty,
        out string aiPartyId,
        out string playerPartyId,
        out bool isPlayerVsPlayer)
    {
        aiParty = null;
        aiPartyId = null;
        playerPartyId = null;
        isPlayerVsPlayer = false;

        var attackerIsPlayer = attacker.MobileParty?.IsPlayerParty() == true;
        var defenderIsPlayer = defender.MobileParty?.IsPlayerParty() == true;
        var attackerInMapEvent = attacker.MapEvent != null;
        var defenderInMapEvent = defender.MapEvent != null;

        // A concluded map event is finalized before every client has to leave its victory screen. Keep those
        // remaining parties unavailable until their MissionLeft removes them from the mission membership.
        if (Patches.EncounterManagerPatches.IsAwaitingMissionExit(attacker) ||
            Patches.EncounterManagerPatches.IsAwaitingMissionExit(defender))
        {
            Logger.Information(
                "[MissionExitGuard] Refused campaign interaction while a party is still leaving its mission. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PlayerUnavailable, request.RequestId));
            return false;
        }

        if ((attackerIsPlayer && !attacker.MobileParty.IsActive) ||
            (defenderIsPlayer && !defender.MobileParty.IsActive))
        {
            Logger.Debug(
                "Rejecting PvP conversation: a player party is inactive. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PlayerUnavailable, request.RequestId));
            return false;
        }

        // PvP: a party joining an existing battle (exactly one side is already in a map event) is allowed through so
        // the joining player's PlayerEncounter can attach to that battle. There is no AI party to hold for a join.
        if (attackerInMapEvent ^ defenderInMapEvent)
        {
            Logger.Debug(
                "Allowing conversation: joining an existing battle. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            return true;
        }

        if (attackerIsPlayer && defenderIsPlayer &&
            (IsInSiege(attacker.MobileParty) || IsInSiege(defender.MobileParty)))
        {
            Logger.Debug(
                "Rejecting PvP conversation: a player is participating in a siege. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PlayerUnavailable, request.RequestId));
            return false;
        }
        
        // PvP: two human players are allowed to open the encounter so they can fight each other. Neither side is AI,
        // so there is nothing to hold; the defending player is shown a "hold on" popup instead.
        if (attackerIsPlayer && defenderIsPlayer)
        {
            // Checks if there is a request to open army menu and executes if true
            if (attacker.MobileParty?.ActualClan?.Kingdom != null
                && attacker.MobileParty?.ActualClan?.Kingdom == defender.MobileParty?.ActualClan?.Kingdom
                && defender.MobileParty?.Army != null
                && defender.MobileParty?.Army?.LeaderParty == defender.MobileParty
                && defender.MobileParty.Army.LeaderParty.AttachedParties.Contains(attacker.MobileParty) == false
                && !request.ArmyTalkEncounter)
            {
                Logger.Debug(
                "Allowing army join. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
                return true;
            }
            // Reject if either player is already conversing with someone else (first interaction wins) — otherwise a
            // third player could open an encounter with a defender already locked in a conversation.
            if (IsConversingWithOther(request.DefenderId, request.AttackerId) ||
                IsConversingWithOther(request.AttackerId, request.DefenderId))
            {
                Logger.Debug(
                    "Rejecting PvP conversation: a party is already conversing with another player. AttackerId={AttackerId}, DefenderId={DefenderId}",
                    request.AttackerId, request.DefenderId);
                network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PartyEngaged, request.RequestId));
                return false;
            }

            Logger.Debug(
                "Starting custom player-party interaction. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            playerPartyInteractionHandler.TryStartSession(requestingPeer, request, attacker, defender);
            return false;
        }

        // Reject: both parties are already in (separate) battles; do not (re)open an encounter conversation.
        if (attackerInMapEvent || defenderInMapEvent)
        {
            Logger.Debug(
                "Rejecting conversation request: a party is already in a map event. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            return false;
        }

        // Identify the AI side; the requester's own party is the player side (player-player was rejected above).
        PartyBase aiSide;
        if (attackerIsPlayer)
        {
            aiSide = defender;
            aiPartyId = request.DefenderId;
            playerPartyId = request.AttackerId;
        }
        else
        {
            aiSide = attacker;
            aiPartyId = request.AttackerId;
            playerPartyId = request.DefenderId;
        }

        aiParty = aiSide.MobileParty;

        Logger.Debug(
        "Allowing conversation. AttackerId={AttackerId}, DefenderId={DefenderId}",
        request.AttackerId, request.DefenderId);

        return true;
    }

    /// <summary>
    /// [Server, game thread] Holds the AI party and replies to allow.
    /// </summary>
    private void HoldAndApprove(
        NetPeer requestingPeer,
        NetworkRequestConversation request,
        string aiPartyId,
        string playerPartyId,
        bool serverDetected)
    {
        if (!objectManager.TryGetObject(aiPartyId, out PartyBase aiPartyBase) || aiPartyBase.MobileParty == null)
        {
            Logger.Debug(
                "Rejecting conversation request: the party no longer resolves. PartyId={PartyId}",
                aiPartyId);
            return;
        }

        if (!objectManager.TryGetObject(playerPartyId, out PartyBase playerPartyBase))
        {
            Logger.Debug(
                "Rejecting conversation request: the player party no longer resolves. PartyId={PartyId}",
                playerPartyId);
            return;
        }

        if (aiPartyBase.MapEvent != null || playerPartyBase.MapEvent != null)
        {
            Logger.Debug(
                "Rejecting conversation request: a party entered a map event while approving. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            return;
        }

        // A map conversation is exclusive to one player, like a settlement conversation. The
        // hostility carve-out that used to live here existed so simultaneous attackers converge on
        // one MapEvent, but it also let two players hold a diplomacy conversation with the same lord
        // and both apply its one-shot outcome. Attackers still converge: once the holder starts the
        // battle, the attackerInMapEvent/defenderInMapEvent branch above approves the contender's
        // retry so it joins that MapEvent.
        if (conversationPartyTracker.IsEngagedByOther(aiPartyId, requestingPeer))
        {
            Logger.Debug(
                "Rejecting conversation request: the party is already conversing with another player. PartyId={PartyId}",
                aiPartyId);
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PartyEngaged, request.RequestId));
            return;
        }

        // A requester cannot replace its own live engagement with another target.
        if (!ConversationPartyHold.TryEngage(
                conversationPartyTracker,
                requestingPeer,
                playerPartyId,
                aiPartyBase.MobileParty,
                aiPartyId,
                serverDetected && request.DefenderId == playerPartyId,
                request.RequestId))
        {
            Logger.Debug(
                "Rejecting conversation request: the party or the requester is already engaged. PartyId={PartyId}",
                aiPartyId);
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PartyEngaged, request.RequestId));
            return;
        }

        SendAllowConversation(requestingPeer, request);
    }

    private static bool AreHostile(PartyBase playerParty, PartyBase aiParty)
    {
        var playerFaction = playerParty?.MapFaction;
        var aiFaction = aiParty?.MapFaction;
        return VillageHostileFactionStanceHelper.HasWarStance(playerFaction, aiFaction);
    }

    /// <summary>[Server] Replies to the requester that the conversation may (re)open.</summary>
    private void SendAllowConversation(NetPeer requestingPeer, NetworkRequestConversation request)
    {
        network.Send(requestingPeer, new NetworkAllowConversation(
            request.DefenderId,
            request.AttackerId,
            request.ForcePlayerOutFromSettlement,
            request.Source,
            request.RequestId));
    }

    /// <summary>
    /// [Server] Records the PvP engagement (attacker peer -> defender party) and, the first time it is seen, broadcasts
    /// <see cref="NetworkPlayerInteractionStarted"/> so the defending player's client shows the "hold on" popup.
    /// Re-broadcasting is skipped on the rate-limited retries of the same request.
    /// </summary>
    private void NotifyPvpInteractionStarted(NetPeer requestingPeer, NetworkRequestConversation request, PartyBase attacker)
    {
        bool shouldDrainNotifications;
        lock (pvpInteractionSync)
        {
            // TryAdd returns false when this attacker already has a recorded interaction; the popup is already up.
            if (pvpDefenderByAttacker.TryGetValue(requestingPeer, out var currentInteraction))
            {
                if (currentInteraction.DefenderPartyId == request.DefenderId)
                    pvpDefenderByAttacker[requestingPeer] = new PvpInteraction(request.DefenderId, request.RequestId);
                return;
            }

            if (!pvpDefenderByAttacker.TryAdd(
                    requestingPeer,
                    new PvpInteraction(request.DefenderId, request.RequestId)))
                return;

            // Mark both players before broadcasting so a synchronous end callback cannot reinsert the pair.
            conversationPartyTracker.BeginPvpConversation(request.AttackerId, request.DefenderId);

            var attackerName = attacker.LeaderHero?.Name?.ToString() ?? attacker.Name?.ToString() ?? "Another player";
            shouldDrainNotifications = EnqueuePvpInteractionNotification(
                new NetworkPlayerInteractionStarted(request.DefenderId, attackerName));
        }

        if (shouldDrainNotifications)
            DrainPvpInteractionNotifications();
    }

    /// <summary>[Server] True when <paramref name="partyId"/> is already in a PvP conversation with someone other than
    /// <paramref name="allowedPartnerId"/> (so the same pair re-requesting is still allowed).</summary>
    private bool IsConversingWithOther(string partyId, string allowedPartnerId)
        => conversationPartyTracker.TryGetPvpPartner(partyId, out var partner) && partner != allowedPartnerId;

    private static bool IsInSiege(MobileParty party)
        => party.BesiegerCamp != null || party.CurrentSettlement?.IsUnderSiege == true;

    /// <summary>
    /// [Server] Ends the given attacker's PvP interaction (the attacker left before any battle), telling the
    /// defending player's client to close its popup and leave the encounter. Once a battle map event exists, every
    /// involved player party — defender and joiners — is instead closed on finalize via
    /// <see cref="Messages.NetworkClosePvpEncounter"/> (see <see cref="BattleHandler"/>).
    /// </summary>
    private void EndPvpInteraction(
        NetPeer attackerPeer,
        string requestId = null,
        bool requireRequestIdMatch = false)
    {
        if (attackerPeer == null) return;

        bool shouldDrainNotifications;
        lock (pvpInteractionSync)
        {
            PvpInteraction interaction;
            if (!pvpDefenderByAttacker.TryGetValue(attackerPeer, out interaction))
                return;

            if (requireRequestIdMatch && interaction.RequestId != requestId)
                return;

            if (!pvpDefenderByAttacker.TryRemove(attackerPeer, out interaction))
                return;

            conversationPartyTracker.EndPvpConversation(interaction.DefenderPartyId);
            shouldDrainNotifications = EnqueuePvpInteractionNotification(
                new NetworkPlayerInteractionEnded(interaction.DefenderPartyId));
        }

        if (shouldDrainNotifications)
            DrainPvpInteractionNotifications();
    }

    /// <summary>
    /// Adds a notification while <see cref="pvpInteractionSync"/> is held. A single caller drains the queue after
    /// releasing the state lock, preserving state-transition order even when a send synchronously triggers another
    /// transition.
    /// </summary>
    private bool EnqueuePvpInteractionNotification(IMessage notification)
    {
        pvpInteractionNotifications.Enqueue(notification);
        if (isDrainingPvpInteractionNotifications)
            return false;

        isDrainingPvpInteractionNotifications = true;
        return true;
    }

    private void DrainPvpInteractionNotifications()
    {
        try
        {
            while (true)
            {
                IMessage notification;
                lock (pvpInteractionSync)
                {
                    if (pvpInteractionNotifications.Count == 0)
                    {
                        isDrainingPvpInteractionNotifications = false;
                        return;
                    }

                    notification = pvpInteractionNotifications.Dequeue();
                }

                network.SendAll(notification);
            }
        }
        catch
        {
            lock (pvpInteractionSync)
            {
                pvpInteractionNotifications.Clear();
                isDrainingPvpInteractionNotifications = false;
            }
            throw;
        }
    }

    /// <summary>[Client] Server approved: re-run RestartPlayerEncounter with the same parameters.</summary>
    private void Handle_NetworkAllowConversation(MessagePayload<NetworkAllowConversation> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            var observedActivationVersion = conversationActivationVersion;

            if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.DefenderId, out var defender))
            {
                ClearPendingConversationRequest(message.RequestId);
                SendConversationEndedToServer(message.RequestId);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.AttackerId, out var attacker))
            {
                ClearPendingConversationRequest(message.RequestId);
                SendConversationEndedToServer(message.RequestId);
                return;
            }

            try
            {
                var restartDecision = restartContextTracker.Consume(
                    message.RequestId,
                    PlayerEncounter.Current,
                    defender,
                    attacker);

                if (restartDecision == ConversationRestartDecision.Duplicate)
                {
                    Logger.Debug("Ignoring duplicate conversation approval for the already-open encounter");
                    ActivateConversationRequest(message.RequestId, observedActivationVersion);
                    return;
                }

                if (restartDecision == ConversationRestartDecision.Stale)
                {
                    Logger.Warning("Ignoring stale conversation approval because the encounter changed after the request");
                    ClearPendingConversationRequest(message.RequestId);
                    SendConversationEndedToServer(message.RequestId);
                    return;
                }

                approvedRestartDepth++;
                try
                {
                    using (new AllowedThread())
                    {
                        if (message.Source == ConversationRestartSource.EncounterManager)
                        {
                            EncounterManager.RestartPlayerEncounter(attacker, defender);
                        }
                        else
                        {
                            // PlayerEncounter.RestartPlayerEncounter(defenderParty, attackerParty, forcePlayerOutFromSettlement)
                            PlayerEncounter.RestartPlayerEncounter(defender, attacker, message.ForcePlayerOutFromSettlement);
                        }
                    }
                }
                finally
                {
                    approvedRestartDepth--;
                }

                ActivateConversationRequest(message.RequestId, observedActivationVersion);
            }
            catch (Exception e)
            {
                // The server engaged and held the AI party before approving; if the restart fails,
                // release that hold so the party does not stay frozen for other players.
                Logger.Error(e, "Failed to restart approved conversation encounter; releasing the server-side party hold");
                ClearPendingConversationRequest(message.RequestId);
                SendConversationEndedToServer(message.RequestId);
            }
        }, context: nameof(Handle_NetworkAllowConversation));
    }

    /// <summary>[Client] This player's encounter finished; tell the server to release the held party.</summary>
    private void Handle_ConversationEnded(MessagePayload<ConversationEnded> payload)
    {
        if (approvedRestartDepth > 0) return;

        var pendingRequestId = pendingConversationRequestId;
        var activeRequestId = activeConversationRequestId;
        var hadActiveConversationRequest = hasActiveConversationRequest;

        pendingConversationRequestId = null;
        activeConversationRequestId = null;
        hasActiveConversationRequest = false;

        // Release every request id the server may still hold; null only covers legacy or server-detected engagements.
        if (pendingRequestId != null)
            SendConversationEndedToServer(pendingRequestId);

        if (hadActiveConversationRequest &&
            (pendingRequestId == null || activeRequestId != pendingRequestId))
            SendConversationEndedToServer(activeRequestId);

        if (pendingRequestId == null && !hadActiveConversationRequest)
            SendConversationEndedToServer(null);
    }

    /// <summary>
    /// [Client] Tell the server this player's conversation is over (the encounter finished, or an approved one
    /// failed to start), so it releases the held party.
    /// </summary>
    private void SendConversationEndedToServer(string requestId)
    {
        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkConversationEnded(requestId));
    }

    /// <summary>[Server] A client's encounter finished: release the AI party held for that player, if any.</summary>
    private void Handle_NetworkConversationEnded(MessagePayload<NetworkConversationEnded> payload)
    {
        if (ModInformation.IsClient) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkConversationEnded));
            return;
        }

        ReleaseEngagementOnMainThread(peer, payload.What.RequestId, requireRequestIdMatch: true);
        EndPvpInteraction(peer, payload.What.RequestId, requireRequestIdMatch: true);
    }

    /// <summary>[Client] The server denied the request; tell the player why.</summary>
    private void Handle_NetworkConversationDenied(MessagePayload<NetworkConversationDenied> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            restartContextTracker.Remove(message.RequestId);
            ClearPendingConversationRequest(message.RequestId);

            if (message.Reason == ConversationDeniedReason.PlayerUnavailable)
                ConversationPartyHold.ShowPlayerUnavailableMessage();
            else
                ConversationPartyHold.ShowInteractionBlockedMessage();
        }, context: nameof(Handle_NetworkConversationDenied));
    }

    private void ActivateConversationRequest(string requestId, long observedActivationVersion)
    {
        if (conversationActivationVersion != observedActivationVersion)
        {
            Logger.Debug("Ignoring older conversation approval because a newer approval activated while it was waiting");
            ClearPendingConversationRequest(requestId);
            SendConversationEndedToServer(requestId);
            return;
        }

        activeConversationRequestId = requestId;
        hasActiveConversationRequest = true;
        conversationActivationVersion++;
        ClearPendingConversationRequest(requestId);
    }

    private void ClearPendingConversationRequest(string requestId)
    {
        if (pendingConversationRequestId == requestId)
            pendingConversationRequestId = null;
    }

    /// <summary>[Server] The defender's client reports it is showing the "hold on" popup; record its peer so a
    /// later disconnect can be mapped back to this conversation.</summary>
    private void Handle_NetworkPvpDefenderShown(MessagePayload<NetworkPvpDefenderShown> payload)
    {
        if (ModInformation.IsClient) return;

        if (payload.Who is NetPeer defenderPeer)
            conversationPartyTracker.SetPvpDefenderPeer(payload.What.DefenderPartyId, defenderPeer);
    }

    /// <summary>[Server] A player disconnected: release the AI party held for them, the PvP interaction they drove
    /// (as attacker), and the PvP conversation they were the defender of.</summary>
    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient) return;

        ReleaseEngagementOnMainThread(payload.What.PlayerId);
        EndPvpInteraction(payload.What.PlayerId);
        EndPvpInteractionForDefender(payload.What.PlayerId);
    }

    /// <summary>[Server] The disconnected peer was a PvP defender: end the conversation and make the attacker (its
    /// partner) leave the encounter, since the party it was interacting with is gone.</summary>
    private void EndPvpInteractionForDefender(NetPeer defenderPeer)
    {
        if (!conversationPartyTracker.TryGetPvpPartyByPeer(defenderPeer, out var defenderPartyId))
            return;

        if (conversationPartyTracker.TryGetPvpPartner(defenderPartyId, out var attackerPartyId))
            network.SendAll(new NetworkClosePvpEncounter(new[] { attackerPartyId }));

        conversationPartyTracker.EndPvpConversation(defenderPartyId);
    }

    /// <summary>[Server] Releases the given player's engagement on the game thread.</summary>
    private void ReleaseEngagementOnMainThread(
        NetPeer peer,
        string requestId = null,
        bool requireRequestIdMatch = false)
    {
        GameThread.Run(() =>
            ConversationPartyHold.EndEngagement(
                conversationPartyTracker,
                peer,
                requestId,
                requireRequestIdMatch));
    }
}
