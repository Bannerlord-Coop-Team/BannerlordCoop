using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
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
/// </remarks>
internal class ConversationRequestHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ConversationRequestHandler>();

    private static readonly TimeSpan RequestCooldown = TimeSpan.FromMilliseconds(500);

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    private DateTime lastRequestSentUtc = DateTime.MinValue;

    public ConversationRequestHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<ConversationRequested>(Handle_ConversationRequested);
        messageBroker.Subscribe<NetworkRequestConversation>(Handle_NetworkRequestConversation);
        messageBroker.Subscribe<NetworkAllowConversation>(Handle_NetworkAllowConversation);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ConversationRequested>(Handle_ConversationRequested);
        messageBroker.Unsubscribe<NetworkRequestConversation>(Handle_NetworkRequestConversation);
        messageBroker.Unsubscribe<NetworkAllowConversation>(Handle_NetworkAllowConversation);
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

        // Reject: a conversation between two human players is not driven through PlayerEncounter.Init.
        if (attacker.MobileParty?.IsPlayerParty() == true && defender.MobileParty?.IsPlayerParty() == true)
        {
            Logger.Debug(
                "Rejecting conversation request: both parties are players. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            return;
        }

        // Reject: a party already in a battle must not (re)open an encounter conversation.
        if (attacker.MapEvent != null || defender.MapEvent != null)
        {
            Logger.Debug(
                "Rejecting conversation request: a party is already in a map event. AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.AttackerId, request.DefenderId);
            return;
        }

        Logger.Debug(
            "Allowing conversation. AttackerId={AttackerId}, DefenderId={DefenderId}",
            request.AttackerId, request.DefenderId);

        network.Send(requestingPeer, new NetworkAllowConversation(request.DefenderId, request.AttackerId, request.ForcePlayerOutFromSettlement, request.Source));
    }

    /// <summary>[Client] Server approved: re-run RestartPlayerEncounter with the same parameters.</summary>
    private void Handle_NetworkAllowConversation(MessagePayload<NetworkAllowConversation> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.DefenderId, out var defender)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.AttackerId, out var attacker)) return;

        if (PlayerEncounter.Current != null)
        {
            Logger.Warning("Conversation allowed but PlayerEncounter.Current is not null; cannot restart encounter");
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
}
