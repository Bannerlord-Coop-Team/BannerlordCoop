using Common.Messaging;
using Common.Network;
using GameInterface.Services.Chat.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.Messages;
using GameInterface.Services.UI.ServerInfo;
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
#if DEBUG
    /// <summary>[Debug] Puts the text in the chat input and runs the Send button's action; false when chat is turned off.</summary>
    bool SubmitForLiveTest(string text, out string state);
#endif
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
#if DEBUG
    private int sentMessageCount;
#endif

    public ChatService(
        INetwork network,
        IPlayerManager playerManager,
        IChatPlayerNameResolver playerNameResolver,
        IControllerIdProvider controllerIdProvider,
        ICoopOptionsStore optionsStore,
        IMessageBroker messageBroker,
        Lazy<IServerInfoService> serverInfo)
    {
        this.network = network;
        this.playerManager = playerManager;
        this.playerNameResolver = playerNameResolver;
        this.controllerIdProvider = controllerIdProvider;
        this.messageBroker = messageBroker;

        // Lazy breaks the construction cycle: the server info panel waits while chat is typing, and
        // chat needs the panel only when !motd is sent.
        viewModel = new ChatVM(
            Send,
            () => controllerIdProvider.ControllerId,
            () => serverInfo.Value.Reopen());
        var showChat = ChatOptionsTabProvider.GetShowChatOrDefault(optionsStore.LoadOrDefault());
        overlay = new ChatOverlay(viewModel, RequestParticipants, showChat);
        messageBroker.Subscribe<ChatVisibilitySelected>(HandleChatVisibilitySelected);
    }

    public bool IsTyping => viewModel.IsOpen;

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

    private void Send(NetworkSendChatMessage message)
    {
        network.SendAll(message);
#if DEBUG
        sentMessageCount++;
#endif
    }

#if DEBUG
    // Goes through the same ActionSend as the Send button, so a live run proves !motd and a chat line
    // without operating-system input. It replaces anything typed and sends on the selected channel.
    public bool SubmitForLiveTest(string text, out string state)
    {
        if (!overlay.IsEnabled)
        {
            state = "Chat is turned off in the co-op options, so nothing was typed or sent.";
            return false;
        }

        int sentBefore = sentMessageCount;
        viewModel.WrittenText = text;
        viewModel.ActionSend();

        string transcript = viewModel.TranscriptText;
        string lastLine = transcript.Substring(transcript.LastIndexOf('\n') + 1);
        state = "Sent: " + (sentMessageCount != sentBefore) +
                "\nChannel: " + viewModel.ActiveChannelText +
                "\nInput: " + (viewModel.WrittenText.Length == 0 ? "empty" : viewModel.WrittenText) +
                "\nLast line: " + (lastLine.Length == 0 ? "none" : lastLine);
        return true;
    }
#endif

    private void HandleChatVisibilitySelected(MessagePayload<ChatVisibilitySelected> payload)
    {
        overlay.SetEnabled(payload.What.ShowChat);
    }
}
