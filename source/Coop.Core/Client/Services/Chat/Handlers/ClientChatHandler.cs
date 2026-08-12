using Common;
using Common.Messaging;
using GameInterface.Services.Chat;
using GameInterface.Services.Chat.Messages;

namespace Coop.Core.Client.Services.Chat.Handlers;

/// <summary>Moves received chat lines onto the game thread for presentation.</summary>
internal sealed class ClientChatHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IChatService chatService;

    public ClientChatHandler(IMessageBroker messageBroker, IChatService chatService)
    {
        this.messageBroker = messageBroker;
        this.chatService = chatService;

        messageBroker.Subscribe<NetworkChatMessage>(HandleChatMessage);
        messageBroker.Subscribe<NetworkChatParticipants>(HandleParticipants);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChatMessage>(HandleChatMessage);
        messageBroker.Unsubscribe<NetworkChatParticipants>(HandleParticipants);
    }

    private void HandleChatMessage(MessagePayload<NetworkChatMessage> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(
            () => chatService.Receive(message),
            context: nameof(ClientChatHandler));
    }

    private void HandleParticipants(MessagePayload<NetworkChatParticipants> payload)
    {
        var participants = payload.What;
        GameThread.RunSafe(
            () => chatService.ReceiveParticipants(participants),
            context: nameof(ClientChatHandler));
    }
}
