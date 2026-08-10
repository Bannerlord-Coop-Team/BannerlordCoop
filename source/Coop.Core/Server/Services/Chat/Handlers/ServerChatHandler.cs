using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Chat;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Core.Server.Services.Chat.Handlers;

/// <summary>Authenticates chat senders and routes global and direct messages.</summary>
internal sealed class ServerChatHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerChatHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IChatPlayerNameResolver playerNameResolver;

    public ServerChatHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IPlayerManager playerManager,
        IChatPlayerNameResolver playerNameResolver)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.playerManager = playerManager;
        this.playerNameResolver = playerNameResolver;

        messageBroker.Subscribe<NetworkSendChatMessage>(HandleChatMessage);
        messageBroker.Subscribe<NetworkRequestChatParticipants>(HandleParticipantRequest);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSendChatMessage>(HandleChatMessage);
        messageBroker.Unsubscribe<NetworkRequestChatParticipants>(HandleParticipantRequest);
    }

    private void HandleChatMessage(MessagePayload<NetworkSendChatMessage> payload)
    {
        if (payload.Who is not NetPeer peer)
        {
            Logger.Warning("Ignoring chat message without an originating peer");
            return;
        }

        var request = payload.What;
        GameThread.RunSafe(
            () => RouteMessage(peer, request),
            context: nameof(ServerChatHandler));
    }

    private void HandleParticipantRequest(MessagePayload<NetworkRequestChatParticipants> payload)
    {
        if (payload.Who is not NetPeer peer)
        {
            Logger.Warning("Ignoring chat participant request without an originating peer");
            return;
        }

        GameThread.RunSafe(
            () => SendParticipants(peer),
            context: nameof(ServerChatHandler));
    }

    internal void SendParticipants(NetPeer peer)
    {
        if (!playerManager.TryGetPlayer(peer, out _))
        {
            Logger.Warning("Ignoring chat participant request from unregistered peer {PeerId}", peer.Id);
            return;
        }

        string[] controllerIds = playerManager.Players
            .Where(playerManager.IsConnected)
            .Select(player => player.ControllerId)
            .ToArray();
        network.SendImmediate(peer, new NetworkChatParticipants(controllerIds));
    }

    internal void RouteMessage(NetPeer senderPeer, NetworkSendChatMessage request)
    {
        if (!playerManager.TryGetPlayer(senderPeer, out var sender))
        {
            Logger.Warning("Ignoring chat message from unregistered peer {PeerId}", senderPeer.Id);
            return;
        }

        if (!TryNormalizeText(request.Text, out var text, out var rejection))
        {
            SendSystemMessage(senderPeer, request.RecipientControllerId, rejection);
            return;
        }

        switch (request.Channel)
        {
            case ChatChannel.Global:
                RouteGlobal(sender, text);
                return;
            case ChatChannel.Direct:
                RouteDirect(senderPeer, sender, request.RecipientControllerId, text);
                return;
            default:
                SendSystemMessage(senderPeer, request.RecipientControllerId, "That chat channel is not available.");
                return;
        }
    }

    private void RouteGlobal(Player sender, string text)
    {
        var message = new NetworkChatMessage(
            ChatChannel.Global,
            sender.ControllerId,
            playerNameResolver.Resolve(sender),
            string.Empty,
            string.Empty,
            text);

        var sentPeers = new HashSet<NetPeer>();
        foreach (var player in playerManager.Players)
        {
            if (playerManager.TryGetPeer(player.ControllerId, out var peer) && sentPeers.Add(peer))
                network.SendImmediate(peer, message);
        }
    }

    private void RouteDirect(NetPeer senderPeer, Player sender, string recipientControllerId, string text)
    {
        if (string.IsNullOrWhiteSpace(recipientControllerId))
        {
            SendSystemMessage(senderPeer, string.Empty, "Choose a player before sending a direct message.");
            return;
        }

        if (string.Equals(sender.ControllerId, recipientControllerId, StringComparison.Ordinal))
        {
            SendSystemMessage(senderPeer, recipientControllerId, "You cannot send a direct message to yourself.");
            return;
        }

        if (!playerManager.TryGetPlayer(recipientControllerId, out var recipient) ||
            !playerManager.TryGetPeer(recipientControllerId, out var recipientPeer))
        {
            SendSystemMessage(senderPeer, recipientControllerId, "That player is not currently connected.");
            return;
        }

        var message = new NetworkChatMessage(
            ChatChannel.Direct,
            sender.ControllerId,
            playerNameResolver.Resolve(sender),
            recipient.ControllerId,
            playerNameResolver.Resolve(recipient),
            text);

        network.SendImmediate(senderPeer, message);
        if (!ReferenceEquals(senderPeer, recipientPeer))
            network.SendImmediate(recipientPeer, message);
    }

    private void SendSystemMessage(NetPeer peer, string recipientControllerId, string text)
    {
        string recipientName = recipientControllerId;
        if (playerManager.TryGetPlayer(recipientControllerId, out var recipient))
            recipientName = playerNameResolver.Resolve(recipient);

        network.SendImmediate(peer, new NetworkChatMessage(
            ChatChannel.System,
            string.Empty,
            "System",
            recipientControllerId ?? string.Empty,
            recipientName ?? string.Empty,
            text));
    }

    private static bool TryNormalizeText(string input, out string text, out string rejection)
    {
        text = string.Empty;
        rejection = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            rejection = "Messages cannot be empty.";
            return false;
        }

        if (input.Length > ChatMessageLimits.MaxMessageLength)
        {
            rejection = $"Messages cannot exceed {ChatMessageLimits.MaxMessageLength} characters.";
            return false;
        }

        var characters = input.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            if (char.IsControl(characters[i])) characters[i] = ' ';
        }

        text = new string(characters).Trim();
        if (text.Length > 0) return true;

        rejection = "Messages cannot be empty.";
        return false;
    }
}
