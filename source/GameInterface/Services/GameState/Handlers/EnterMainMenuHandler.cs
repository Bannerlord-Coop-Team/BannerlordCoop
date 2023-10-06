using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.GameState.Handlers;

internal class EnterMainMenuHandler : IHandler
{
    private readonly IGameStateInterface gameStateInterface;
    private readonly IMessageBroker messageBroker;

    public EnterMainMenuHandler(IGameStateInterface gameStateInterface, IMessageBroker messageBroker)
    {
        this.gameStateInterface = gameStateInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<EnterMainMenu>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<EnterMainMenu>(Handle);
    }

    private void Handle(MessagePayload<EnterMainMenu> payload)
    {
        gameStateInterface.EnterMainMenu();

        messageBroker.Respond(payload.Who, new EnterMainMenuResponse());

        messageBroker.Publish(this, new MainMenuEntered());
    }
}
