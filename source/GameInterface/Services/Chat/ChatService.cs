using Common.Messaging;
using Common.Network;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.UI;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Chat;

public interface IChatService : IGameAbstraction
{
    bool IsTyping { get; }
    void Initialize();
    void Receive(NetworkChatMessage message);
    void ReceiveParticipants(NetworkChatParticipants participants);
}

/// <summary>Owns the client chat view model and overlay for one co-op session.</summary>
public sealed class ChatService : IChatService, IDisposable
{
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IChatPlayerNameResolver playerNameResolver;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IChatFactionMembership factionMembership;
    private readonly IMessageBroker messageBroker;
    private readonly IChatEventLog eventLog;
    private readonly ChatVM viewModel;
    private readonly ChatOverlay overlay;

    public ChatService(
        INetwork network,
        IPlayerManager playerManager,
        IChatPlayerNameResolver playerNameResolver,
        IControllerIdProvider controllerIdProvider,
        IChatFactionMembership factionMembership,
        ICoopOptionsStore optionsStore,
        IMessageBroker messageBroker,
        IChatEventLog eventLog,
        IPlayerKillFeedColorService killFeedColorService)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        if (playerManager == null) throw new ArgumentNullException(nameof(playerManager));
        if (playerNameResolver == null) throw new ArgumentNullException(nameof(playerNameResolver));
        if (controllerIdProvider == null) throw new ArgumentNullException(nameof(controllerIdProvider));
        if (factionMembership == null) throw new ArgumentNullException(nameof(factionMembership));
        if (optionsStore == null) throw new ArgumentNullException(nameof(optionsStore));
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (eventLog == null) throw new ArgumentNullException(nameof(eventLog));
        if (killFeedColorService == null) throw new ArgumentNullException(nameof(killFeedColorService));

        this.network = network;
        this.playerManager = playerManager;
        this.playerNameResolver = playerNameResolver;
        this.controllerIdProvider = controllerIdProvider;
        this.factionMembership = factionMembership;
        this.messageBroker = messageBroker;
        this.eventLog = eventLog;

        viewModel = new ChatVM(
            message => network.SendAll(message),
            () => controllerIdProvider.ControllerId,
            killFeedColorService.GetColor);
        var showChat = ChatOptionsTabProvider.GetShowChatOrDefault(optionsStore.LoadOrDefault());
        overlay = new ChatOverlay(viewModel, RefreshOnOpen, showChat);
        messageBroker.Subscribe<ChatVisibilitySelected>(HandleChatVisibilitySelected);
    }

    public bool IsTyping => overlay.IsInputFocused;

    public void Initialize()
    {
        eventLog.Start(viewModel.ReceiveEvent);
        overlay.Initialize();
    }

    public void Receive(NetworkChatMessage message)
    {
        viewModel.Receive(message);
    }

    public void ReceiveParticipants(NetworkChatParticipants message)
    {
        var participants = new List<(string ControllerId, string DisplayName)>();
        foreach (var controllerId in message.ControllerIds ?? Array.Empty<string>())
        {
            if (string.Equals(controllerId, controllerIdProvider.ControllerId, StringComparison.Ordinal))
                continue;

            string displayName = controllerId;
            if (playerManager.TryGetPlayer(controllerId, out var player))
                displayName = playerNameResolver.Resolve(player);

            participants.Add((controllerId, displayName));
        }

        viewModel.SetParticipants(participants.OrderBy(
            participant => participant.DisplayName,
            StringComparer.OrdinalIgnoreCase));
        RefreshGroupChannels();
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChatVisibilitySelected>(HandleChatVisibilitySelected);
        eventLog.Dispose();
        overlay.Dispose();
    }

    internal bool IsPlayerChatEnabled => overlay.IsPlayerChatEnabled;

    internal void RequestParticipants()
    {
        network.SendAll(new NetworkRequestChatParticipants());
    }

    private void RefreshOnOpen()
    {
        RefreshGroupChannels();
        RequestParticipants();
    }

    private void RefreshGroupChannels()
    {
        bool inClan = false;
        bool inKingdom = false;
        if (playerManager.TryGetPlayer(controllerIdProvider.ControllerId, out var player))
        {
            inClan = factionMembership.TryGetClan(player, out _);
            inKingdom = factionMembership.TryGetKingdom(player, out _);
        }

        viewModel.SetGroupChannelMembership(inClan, inKingdom);
    }

    private void HandleChatVisibilitySelected(MessagePayload<ChatVisibilitySelected> payload)
    {
        overlay.SetPlayerChatEnabled(payload.What.ShowChat);
    }
}
