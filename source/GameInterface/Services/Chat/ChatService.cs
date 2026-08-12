using Common.Messaging;
using Common.Network;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Chat;

public interface IChatService : IGameAbstraction
{
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
    private readonly IMessageBroker messageBroker;
    private readonly ChatVM viewModel;
    private readonly ChatOverlay overlay;

    public ChatService(
        INetwork network,
        IPlayerManager playerManager,
        IChatPlayerNameResolver playerNameResolver,
        IControllerIdProvider controllerIdProvider,
        ICoopOptionsStore optionsStore,
        IMessageBroker messageBroker)
    {
        this.network = network;
        this.playerManager = playerManager;
        this.playerNameResolver = playerNameResolver;
        this.controllerIdProvider = controllerIdProvider;
        this.messageBroker = messageBroker;

        viewModel = new ChatVM(message => network.SendAll(message), () => controllerIdProvider.ControllerId);
        var showChat = ChatOptionsTabProvider.GetShowChatOrDefault(optionsStore.LoadOrDefault());
        overlay = new ChatOverlay(viewModel, RequestParticipants, showChat);
        messageBroker.Subscribe<ChatVisibilitySelected>(HandleChatVisibilitySelected);
    }

    public void Initialize()
    {
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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChatVisibilitySelected>(HandleChatVisibilitySelected);
        overlay.Dispose();
    }

    internal bool IsChatEnabled => overlay.IsEnabled;

    internal void RequestParticipants()
    {
        network.SendAll(new NetworkRequestChatParticipants());
    }

    private void HandleChatVisibilitySelected(MessagePayload<ChatVisibilitySelected> payload)
    {
        overlay.SetEnabled(payload.What.ShowChat);
    }
}
