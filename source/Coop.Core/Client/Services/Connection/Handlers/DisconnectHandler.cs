using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Common;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.Services.Connection.Handlers;

internal class DisconnectHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ICoopFinalizer coopFinalizer;
    private readonly IGameStateInterface gameStateInterface;

    public DisconnectHandler(IMessageBroker messageBroker, ICoopFinalizer coopFinalizer, IGameStateInterface gameStateInterface) 
    {
        this.messageBroker = messageBroker;
        this.coopFinalizer = coopFinalizer;
        this.gameStateInterface = gameStateInterface;
        messageBroker.Subscribe<NetworkDisconnected>(Handle);
        messageBroker.Subscribe<MainMenuEntered>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkDisconnected>(Handle);
        messageBroker.Unsubscribe<MainMenuEntered>(Handle);
    }

    private void Handle(MessagePayload<NetworkDisconnected> obj)
    {
        gameStateInterface.GoToMainMenu();
    }

    private void Handle(MessagePayload<MainMenuEntered> obj)
    {
        coopFinalizer.Finalize("You have been Disconnected");
    }
}
