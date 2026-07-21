using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Heroes.Extensions;
#if DEBUG
using GameInterface.Services.Locations.Conversations.Commands;
#endif
using GameInterface.Services.Locations.Conversations.Patches;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Services.Locations.Conversations.Handlers;

/// <summary>
/// Bridges the client-side location-conversation acquire/release to the server-authoritative
/// <see cref="LocationConversationTracker"/>.
/// </summary>
/// <remarks>
/// Client: turns a <see cref="LocationConversationRequested"/> into a network request (rate-limited), starts
/// the held-back conversation on approval, and shows a busy message on denial.
/// Server: records the engagement and replies allow/deny; releases the NPC when the client reports the
/// conversation ended or disconnects.
/// </remarks>
internal class LocationConversationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationConversationHandler>();

    private static readonly TimeSpan BlockedMessageCooldown = TimeSpan.FromSeconds(5);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly LocationConversationTracker tracker;
    private readonly IPlayerManager playerManager;
    private readonly ConcurrentDictionary<NetPeer, string> waitingPartyByInitiator = new ConcurrentDictionary<NetPeer, string>();

    private DateTime lastBlockedMessageUtc = DateTime.MinValue;

    public LocationConversationHandler(
        IMessageBroker messageBroker,
        INetwork network,
        LocationConversationTracker tracker,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.tracker = tracker;
        this.playerManager = playerManager;

        messageBroker.Subscribe<LocationConversationRequested>(Handle_LocationConversationRequested);
        messageBroker.Subscribe<LocationConversationEnded>(Handle_LocationConversationEnded);
        messageBroker.Subscribe<NetworkRequestLocationConversation>(Handle_NetworkRequestLocationConversation);
        messageBroker.Subscribe<NetworkAllowLocationConversation>(Handle_NetworkAllowLocationConversation);
        messageBroker.Subscribe<NetworkLocationConversationDenied>(Handle_NetworkLocationConversationDenied);
        messageBroker.Subscribe<NetworkLocationConversationEnded>(Handle_NetworkLocationConversationEnded);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LocationConversationRequested>(Handle_LocationConversationRequested);
        messageBroker.Unsubscribe<LocationConversationEnded>(Handle_LocationConversationEnded);
        messageBroker.Unsubscribe<NetworkRequestLocationConversation>(Handle_NetworkRequestLocationConversation);
        messageBroker.Unsubscribe<NetworkAllowLocationConversation>(Handle_NetworkAllowLocationConversation);
        messageBroker.Unsubscribe<NetworkLocationConversationDenied>(Handle_NetworkLocationConversationDenied);
        messageBroker.Unsubscribe<NetworkLocationConversationEnded>(Handle_NetworkLocationConversationEnded);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);

        waitingPartyByInitiator.Clear();
#if DEBUG
        LocationConversationLiveTestProbe.Disable();
#endif
    }

    /// <summary>[Client] Forward the request to the server.</summary>
    private void Handle_LocationConversationRequested(MessagePayload<LocationConversationRequested> payload)
    {
        var request = payload.What;

        // The acquire patch arms a single pending request at a time and blocks re-entry until it resolves,
        // so no rate-limiting is needed here - and a cooldown could otherwise swallow a legitimate next
        // request after a fast denial, wedging all further interaction.
        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestLocationConversation(request.LocationId, request.CharacterId, request.Generation));
    }

    /// <summary>[Server] Record the engagement and reply allow, or deny when the NPC is already taken.</summary>
    private void Handle_NetworkRequestLocationConversation(MessagePayload<NetworkRequestLocationConversation> payload)
    {
        if (!ModInformation.IsServer) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkRequestLocationConversation));
            return;
        }

        var request = payload.What;
        if (!playerManager.TryGetPlayer(peer, out var player) || string.IsNullOrEmpty(player.CharacterObjectId))
        {
            Logger.Error("Could not resolve the player character for {Message}", nameof(NetworkRequestLocationConversation));
            network.Send(peer, new NetworkLocationConversationDenied(request.Generation));
            return;
        }

        var engagerNpcKey = LocationConversationTracker.ComposeKey(request.LocationId, player.CharacterObjectId);
        var targetNpcKey = LocationConversationTracker.ComposeKey(request.LocationId, request.CharacterId);

        // Reserve both participants so crossed requests cannot approve two conversations at once.
        if (tracker.TryBeginEngagement(peer, engagerNpcKey, targetNpcKey))
        {
            network.Send(peer, new NetworkAllowLocationConversation(request.Generation));
            StartPlayerWaitingInteraction(peer, request.CharacterId);
        }
        else
        {
            network.Send(peer, new NetworkLocationConversationDenied(request.Generation));
        }
    }

    /// <summary>[Client] Server approved: start the held-back conversation on the main thread.</summary>
    private void Handle_NetworkAllowLocationConversation(MessagePayload<NetworkAllowLocationConversation> payload)
    {
        if (ModInformation.IsServer) return;

        var generation = payload.What.Generation;
#if DEBUG
        LocationConversationLiveTestProbe.RecordAllowed(generation);
#endif
        GameThread.Run(() => LocationConversationPatches.StartApprovedConversation(generation));
    }

    /// <summary>[Client] Server denied: drop the pending request and tell the player why.</summary>
    private void Handle_NetworkLocationConversationDenied(MessagePayload<NetworkLocationConversationDenied> payload)
    {
        if (ModInformation.IsServer) return;

        var generation = payload.What.Generation;
#if DEBUG
        LocationConversationLiveTestProbe.RecordDenied(generation);
#endif
        GameThread.Run(() =>
        {
            // Only explain the refusal if this denial still matches our current pending request; a stale denial
            // (the player left and started another) neither clears the new pending nor pops a message.
            var shouldShowBlockedMessage = LocationConversationPatches.CancelPending(generation);
#if DEBUG
            shouldShowBlockedMessage |= LocationConversationLiveTestProbe.Enabled;
#endif
            if (shouldShowBlockedMessage)
            {
                ShowInteractionBlockedMessage();
            }
        });
    }

    /// <summary>[Client] This player's conversation ended; tell the server to release the NPC.</summary>
    private void Handle_LocationConversationEnded(MessagePayload<LocationConversationEnded> payload)
    {
        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkLocationConversationEnded());
    }

    /// <summary>[Server] A client's conversation finished: release the NPC held for that player, if any.</summary>
    private void Handle_NetworkLocationConversationEnded(MessagePayload<NetworkLocationConversationEnded> payload)
    {
        if (!ModInformation.IsServer) return;

        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkLocationConversationEnded));
            return;
        }

        tracker.TryEndEngagement(peer, out _);
        EndPlayerWaitingInteraction(peer);
    }

    /// <summary>[Server] A player disconnected: release the NPC held for them, if any.</summary>
    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (!ModInformation.IsServer) return;

        tracker.TryEndEngagement(payload.What.PlayerId, out _);
        EndPlayerWaitingInteraction(payload.What.PlayerId);
    }

    private void StartPlayerWaitingInteraction(NetPeer initiatorPeer, string characterId)
    {
        if (!tracker.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character)) return;

        var targetHero = character.HeroObject;
        if (targetHero?.IsPlayerHero() != true) return;

        var targetParty = targetHero.PartyBelongedTo?.Party;
        if (targetParty?.MobileParty?.IsPlayerParty() != true) return;
        if (!tracker.ObjectManager.TryGetId(targetParty, out var targetPartyId)) return;
        if (!waitingPartyByInitiator.TryAdd(initiatorPeer, targetPartyId)) return;

        network.SendAll(new NetworkPlayerInteractionStarted(targetPartyId, GetPlayerName(initiatorPeer), isLocationInteraction: true));
    }

    private void EndPlayerWaitingInteraction(NetPeer initiatorPeer)
    {
        if (initiatorPeer == null) return;
        if (!waitingPartyByInitiator.TryRemove(initiatorPeer, out var targetPartyId)) return;

        network.SendAll(new NetworkPlayerInteractionEnded(targetPartyId, isLocationInteraction: true));
    }

    private string GetPlayerName(NetPeer peer)
    {
        if (!playerManager.TryGetPlayer(peer, out var player)) return "Another player";
        if (!tracker.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero)) return "Another player";

        return hero.Name?.ToString() ?? "Another player";
    }

    /// <summary>
    /// Shows the local player why their interaction did nothing, at most once per cooldown so a repeatedly
    /// retried click does not flood the log. Must run on the game's main thread.
    /// </summary>
    private void ShowInteractionBlockedMessage()
    {
        var now = DateTime.UtcNow;
        if (now - lastBlockedMessageUtc < BlockedMessageCooldown) return;
        lastBlockedMessageUtc = now;

        InformationManager.DisplayMessage(new InformationMessage(
            "You cannot talk to this character while another player is talking to them"));
    }
}
