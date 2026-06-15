using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using LiteNetLib;
using Serilog;
using System;
using System.Reflection;
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
/// Server (additionally): while a player's conversation is open, the AI party is held in place and marked engaged
/// in <see cref="ConversationPartyTracker"/> (requests against it from other players are rejected); the hold is
/// released when the client reports the encounter finished, fails to start it, or disconnects.
/// </remarks>
internal class ConversationRequestHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ConversationRequestHandler>();

    private static readonly TimeSpan RequestCooldown = TimeSpan.FromMilliseconds(500);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ConversationPartyTracker conversationPartyTracker;

    private DateTime lastRequestSentUtc = DateTime.MinValue;

    public ConversationRequestHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ConversationPartyTracker conversationPartyTracker)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.conversationPartyTracker = conversationPartyTracker;

        messageBroker.Subscribe<ConversationRequested>(Handle_ConversationRequested);
        messageBroker.Subscribe<NetworkRequestConversation>(Handle_NetworkRequestConversation);
        messageBroker.Subscribe<NetworkAllowConversation>(Handle_NetworkAllowConversation);
        messageBroker.Subscribe<ConversationEnded>(Handle_ConversationEnded);
        messageBroker.Subscribe<NetworkConversationEnded>(Handle_NetworkConversationEnded);
        messageBroker.Subscribe<NetworkConversationDenied>(Handle_NetworkConversationDenied);
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
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    /// <summary>[Client] Rate-limit, resolve ids, and forward the request to the server.</summary>
    private void Handle_ConversationRequested(MessagePayload<ConversationRequested> payload)
    {
        var now = DateTime.UtcNow;
        if (now - lastRequestSentUtc < RequestCooldown)
            return; // drop: at most one request per cooldown window

        var request = payload.What;

        if (!objectManager.TryGetIdWithLogging(request.DefenderParty, out var defenderId)) return;
        if (!objectManager.TryGetIdWithLogging(request.AttackerParty, out var attackerId)) return;

        lastRequestSentUtc = now;

        Logger.Debug("Requesting conversation from server. AttackerId={AttackerId}, DefenderId={DefenderId}", attackerId, defenderId);

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestConversation(defenderId, attackerId, request.ForcePlayerOutFromSettlement, request.Source));
    }

    /// <summary>[Server] Validate the request; reply to allow, or stay silent to reject.</summary>
    private void Handle_NetworkRequestConversation(MessagePayload<NetworkRequestConversation> payload)
    {
        if (!ModInformation.IsServer) return;

        var request = payload.What;

        if (!(payload.Who is NetPeer requestingPeer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkRequestConversation));
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<PartyBase>(request.AttackerId, out var attacker)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(request.DefenderId, out var defender)) return;

        if (!TryAcceptConversationRequest(requestingPeer, request, attacker, defender, out var aiParty, out var aiPartyId, out var playerPartyId))
            return;

        if (aiParty == null)
        {
            // No AI mobile party involved (e.g. a settlement side); nothing to hold.
            SendAllowConversation(requestingPeer, request);
            return;
        }

        // Mark and hold on the game thread, then reply, so the party is frozen and protected before the client
        // (re)opens the encounter. The filter above runs on the network thread; game state can change while the
        // approval is queued, so the hold step re-validates everything it depends on.
        GameLoopRunner.RunOnMainThread(() => HoldAndApprove(requestingPeer, request, aiPartyId, playerPartyId));
    }

    /// <summary>
    /// [Server] Network-thread filter that rejects when both parties are players or either is already in a battle,
    /// and identifies the AI side. Returns false (rejection logged, and the requester told when the party is engaged
    /// by another player) when the request must not proceed. On success <paramref name="aiParty"/> is the AI mobile
    /// party to hold, or null when only a settlement side is involved (allow, but nothing to hold).
    /// </summary>
    private bool TryAcceptConversationRequest(
        NetPeer requestingPeer,
        NetworkRequestConversation request,
        PartyBase attacker,
        PartyBase defender,
        out MobileParty aiParty,
        out string aiPartyId,
        out string playerPartyId)
    {
        aiParty = null;
        aiPartyId = null;
        playerPartyId = null;

        var attackerIsPlayer = attacker.MobileParty?.IsPlayerParty() == true;
        var defenderIsPlayer = defender.MobileParty?.IsPlayerParty() == true;

        // Reject: a conversation between two human players is not driven through PlayerEncounter.Init.
        if (attackerIsPlayer && defenderIsPlayer)
        {
            Logger.Debug(
                "Rejecting conversation request: both parties are players. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            return false;
        }

        // Reject: a party already in a battle must not (re)open an encounter conversation.
        if (attacker.MapEvent != null || defender.MapEvent != null)
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

        if (aiParty != null && conversationPartyTracker.IsEngagedByOther(aiPartyId, requestingPeer))
        {
            Logger.Debug(
                "Rejecting conversation request: the party is in a conversation with another player. PartyId={PartyId}",
                aiPartyId);
            network.Send(requestingPeer, new NetworkConversationDenied());
            return false;
        }

        Logger.Debug(
            "Allowing conversation. AttackerId={AttackerId}, DefenderId={DefenderId}",
            request.AttackerId, request.DefenderId);

        return true;
    }

    /// <summary>
    /// [Server, game thread] Re-validate (state can change while the approval is queued), hold the AI party, and
    /// reply to allow. Stays silent (rejecting) when a party no longer resolves or entered a battle meanwhile, and
    /// tells the requester when the party or the requester became engaged in the interim (first approval wins).
    /// </summary>
    private void HoldAndApprove(NetPeer requestingPeer, NetworkRequestConversation request, string aiPartyId, string playerPartyId)
    {
        // Re-resolve on the game thread: either party may have been removed since the network-thread lookup.
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

        // Re-check map events: a party may have entered a battle while the approval was queued.
        if (aiPartyBase.MapEvent != null || playerPartyBase.MapEvent != null)
        {
            Logger.Debug(
                "Rejecting conversation request: a party entered a map event while approving. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            return;
        }

        // Re-check the engagement: another player may have engaged the party meanwhile, or the requester may
        // still hold an unfinished engagement with a different party (first approval wins).
        if (!ConversationPartyHold.TryEngage(conversationPartyTracker, requestingPeer, playerPartyId, aiPartyBase.MobileParty, aiPartyId))
        {
            Logger.Debug(
                "Rejecting conversation request: the party or the requester is already engaged. PartyId={PartyId}",
                aiPartyId);
            network.Send(requestingPeer, new NetworkConversationDenied());
            return;
        }

        SendAllowConversation(requestingPeer, request);
    }

    /// <summary>[Server] Replies to the requester that the conversation may (re)open.</summary>
    private void SendAllowConversation(NetPeer requestingPeer, NetworkRequestConversation request)
    {
        network.Send(requestingPeer, new NetworkAllowConversation(request.DefenderId, request.AttackerId, request.ForcePlayerOutFromSettlement, request.Source));
    }

    /// <summary>[Client] Server approved: re-run RestartPlayerEncounter with the same parameters.</summary>
    private void Handle_NetworkAllowConversation(MessagePayload<NetworkAllowConversation> payload)
    {
        var message = payload.What;

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

        // Restarting the encounter reopens the encounter menu and reads PlayerEncounter state
        // the main-thread tick mutates; both must run on the main thread, not the network
        // thread that delivered the approval.
        GameLoopRunner.RunOnMainThread(() =>
        {
            if (Campaign.Current == null) return;

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
            catch (Exception e)
            {
                // The server engaged and held the AI party before approving; if the restart fails,
                // release that hold so the party does not stay frozen for other players.
                Logger.Error(e, "Failed to restart approved conversation encounter; releasing the server-side party hold");
                SendConversationEndedToServer();
            }
        });
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
        if (!ModInformation.IsServer) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkConversationEnded));
            return;
        }

        ReleaseEngagementOnMainThread(peer);
    }

    /// <summary>[Client] The server denied the request because the party is engaged; tell the player why.</summary>
    private void Handle_NetworkConversationDenied(MessagePayload<NetworkConversationDenied> payload)
    {
        if (ModInformation.IsServer) return;

        GameLoopRunner.RunOnMainThread(ConversationPartyHold.ShowInteractionBlockedMessage);
    }

    /// <summary>[Server] A player disconnected: release the AI party held for them, if any.</summary>
    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (!ModInformation.IsServer) return;

        ReleaseEngagementOnMainThread(payload.What.PlayerId);
    }

    /// <summary>[Server] Releases the given player's engagement on the game thread.</summary>
    private void ReleaseEngagementOnMainThread(NetPeer peer)
    {
        GameLoopRunner.RunOnMainThread(() =>
            ConversationPartyHold.EndEngagement(conversationPartyTracker, peer));
    }
}
