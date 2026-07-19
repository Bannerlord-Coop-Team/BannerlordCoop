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
/// Server (additionally): while a conversation is open, the AI party is held in place. Hostile players may share
/// that hold so simultaneous attack attempts can converge on one MapEvent.
/// </remarks>
internal class ConversationRequestHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ConversationRequestHandler>();

    private static readonly TimeSpan RequestCooldown = TimeSpan.FromMilliseconds(500);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly PlayerPartyInteractionHandler playerPartyInteractionHandler;
    private readonly IPlayerManager playerManager;

    private DateTime lastRequestSentUtc = DateTime.MinValue;

    // [Server] Player-vs-player interactions in progress, keyed by the attacking player's peer -> the defending
    // player's party id. Lets the defender be told when the interaction ends (the attacker has no AI party to hold,
    // so this is the only record of a PvP engagement). The defender's "hold on" popup is driven from these broadcasts.
    private readonly ConcurrentDictionary<NetPeer, string> pvpDefenderByAttacker = new ConcurrentDictionary<NetPeer, string>();

    public ConversationRequestHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ConversationPartyTracker conversationPartyTracker,
        PlayerPartyInteractionHandler playerPartyInteractionHandler,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.conversationPartyTracker = conversationPartyTracker;
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

        Logger.Debug("Requesting conversation from server. AttackerId={AttackerId}, DefenderId={DefenderId}", attackerId, defenderId);

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestConversation(defenderId, attackerId, request.ForcePlayerOutFromSettlement, request.Source));
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
                request.Source));
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
            () => ProcessConversationRequest(requestingPeer, request),
            context: nameof(Handle_NetworkRequestConversation));
    }

    private void ProcessConversationRequest(NetPeer requestingPeer, NetworkRequestConversation request)
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

        HoldAndApprove(requestingPeer, request, aiPartyId, playerPartyId);
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
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PlayerUnavailable));
            return false;
        }

        if ((attackerIsPlayer && !attacker.MobileParty.IsActive) ||
            (defenderIsPlayer && !defender.MobileParty.IsActive))
        {
            Logger.Debug(
                "Rejecting PvP conversation: a player party is inactive. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PlayerUnavailable));
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

        // PvP: two human players are allowed to open the encounter so they can fight each other. Neither side is AI,
        // so there is nothing to hold; the defending player is shown a "hold on" popup instead.
        if (attackerIsPlayer && defenderIsPlayer)
        {
            // Reject if either player is already conversing with someone else (first interaction wins) — otherwise a
            // third player could open an encounter with a defender already locked in a conversation.
            if (IsConversingWithOther(request.DefenderId, request.AttackerId) ||
                IsConversingWithOther(request.AttackerId, request.DefenderId))
            {
                Logger.Debug(
                    "Rejecting PvP conversation: a party is already conversing with another player. AttackerId={AttackerId}, DefenderId={DefenderId}",
                    request.AttackerId, request.DefenderId);
                network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PartyEngaged));
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
    private void HoldAndApprove(NetPeer requestingPeer, NetworkRequestConversation request, string aiPartyId, string playerPartyId)
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

        if (conversationPartyTracker.IsEngagedByOther(aiPartyId, requestingPeer) &&
            !AreHostile(playerPartyBase, aiPartyBase))
        {
            Logger.Debug(
                "Rejecting shared conversation for a non-hostile party. PartyId={PartyId}",
                aiPartyId);
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PartyEngaged));
            return;
        }

        // A requester may share this hostile target but cannot replace its own live engagement with another target.
        if (!ConversationPartyHold.TryEngage(conversationPartyTracker, requestingPeer, playerPartyId, aiPartyBase.MobileParty, aiPartyId))
        {
            Logger.Debug(
                "Rejecting conversation request: the party or the requester is already engaged. PartyId={PartyId}",
                aiPartyId);
            network.Send(requestingPeer, new NetworkConversationDenied(ConversationDeniedReason.PartyEngaged));
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
        network.Send(requestingPeer, new NetworkAllowConversation(request.DefenderId, request.AttackerId, request.ForcePlayerOutFromSettlement, request.Source));
    }

    /// <summary>
    /// [Server] Records the PvP engagement (attacker peer -> defender party) and, the first time it is seen, broadcasts
    /// <see cref="NetworkPlayerInteractionStarted"/> so the defending player's client shows the "hold on" popup.
    /// Re-broadcasting is skipped on the rate-limited retries of the same request.
    /// </summary>
    private void NotifyPvpInteractionStarted(NetPeer requestingPeer, NetworkRequestConversation request, PartyBase attacker)
    {
        // TryAdd returns false when this attacker already has a recorded interaction; the popup is already up.
        if (!pvpDefenderByAttacker.TryAdd(requestingPeer, request.DefenderId))
            return;

        var attackerName = attacker.LeaderHero?.Name?.ToString() ?? attacker.Name?.ToString() ?? "Another player";

        network.SendAll(new NetworkPlayerInteractionStarted(request.DefenderId, attackerName));

        // Mark both players as conversing so no other party can interact with them while the (no-map-event-yet)
        // conversation is open; the interaction guards consult the tracker. Released in EndPvpInteraction.
        conversationPartyTracker.BeginPvpConversation(request.AttackerId, request.DefenderId);
    }

    /// <summary>[Server] True when <paramref name="partyId"/> is already in a PvP conversation with someone other than
    /// <paramref name="allowedPartnerId"/> (so the same pair re-requesting is still allowed).</summary>
    private bool IsConversingWithOther(string partyId, string allowedPartnerId)
        => conversationPartyTracker.TryGetPvpPartner(partyId, out var partner) && partner != allowedPartnerId;

    /// <summary>
    /// [Server] Ends the given attacker's PvP interaction (the attacker left before any battle), telling the
    /// defending player's client to close its popup and leave the encounter. Once a battle map event exists, every
    /// involved player party — defender and joiners — is instead closed on finalize via
    /// <see cref="Messages.NetworkClosePvpEncounter"/> (see <see cref="BattleHandler"/>).
    /// </summary>
    private void EndPvpInteraction(NetPeer attackerPeer)
    {
        if (attackerPeer == null) return;

        if (pvpDefenderByAttacker.TryRemove(attackerPeer, out var defenderPartyId))
        {
            network.SendAll(new NetworkPlayerInteractionEnded(defenderPartyId));
            conversationPartyTracker.EndPvpConversation(defenderPartyId);
        }
    }

    /// <summary>[Client] Server approved: re-run RestartPlayerEncounter with the same parameters.</summary>
    private void Handle_NetworkAllowConversation(MessagePayload<NetworkAllowConversation> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.DefenderId, out var defender))
            {
                SendConversationEndedToServer();
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.AttackerId, out var attacker))
            {
                SendConversationEndedToServer();
                return;
            }

            try
            {
                if (PlayerEncounter.Current != null)
                {
                    // A duplicate approval for the encounter that is already open (the rate-limited request was retried
                    // while the first approval was in flight): ignore it. Sending ConversationEnded here would release
                    // the server-side hold while the conversation is still active.
                    if (PlayerEncounter.EncounteredParty == defender || PlayerEncounter.EncounteredParty == attacker)
                    {
                        Logger.Debug("Ignoring duplicate conversation approval for the already-open encounter");
                        return;
                    }

                    Logger.Warning("Conversation allowed but PlayerEncounter.Current is not null; cannot restart encounter");
                    SendConversationEndedToServer();
                    return;
                }

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
            catch (Exception e)
            {
                // The server engaged and held the AI party before approving; if the restart fails,
                // release that hold so the party does not stay frozen for other players.
                Logger.Error(e, "Failed to restart approved conversation encounter; releasing the server-side party hold");
                SendConversationEndedToServer();
            }
        }, context: nameof(Handle_NetworkAllowConversation));
    }

    /// <summary>[Client] This player's encounter finished; tell the server to release the held party.</summary>
    private void Handle_ConversationEnded(MessagePayload<ConversationEnded> payload)
    {
        SendConversationEndedToServer();
    }

    /// <summary>
    /// [Client] Tell the server this player's conversation is over (the encounter finished, or an approved one
    /// failed to start), so it releases the held party.
    /// </summary>
    private void SendConversationEndedToServer()
    {
        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkConversationEnded());
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

        ReleaseEngagementOnMainThread(peer);
        EndPvpInteraction(peer);
    }

    /// <summary>[Client] The server denied the request; tell the player why.</summary>
    private void Handle_NetworkConversationDenied(MessagePayload<NetworkConversationDenied> payload)
    {
        if (ModInformation.IsServer) return;

        Action showMessage = payload.What.Reason == ConversationDeniedReason.PlayerUnavailable
            ? ConversationPartyHold.ShowPlayerUnavailableMessage
            : ConversationPartyHold.ShowInteractionBlockedMessage;
        GameThread.RunSafe(showMessage, context: "Show conversation denied");
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
    private void ReleaseEngagementOnMainThread(NetPeer peer)
    {
        GameThread.Run(() =>
            ConversationPartyHold.EndEngagement(conversationPartyTracker, peer));
    }
}
