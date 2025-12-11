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

        // Listen for EnterMainMenu command to safely end current game and transition UI.
        messageBroker.Subscribe<EnterMainMenu>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<EnterMainMenu>(Handle);
    }

    private void Handle(MessagePayload<EnterMainMenu> payload)
    {
        // Track whether a game was active; only publish responses/events when applicable.
        bool hadGame = TaleWorlds.Core.Game.Current != null;
        gameStateInterface.EnterMainMenu();

        if (hadGame)
        {
            messageBroker.Respond(payload.Who, new EnterMainMenuResponse());
        }

        if (hadGame && !GameStateInterface.IsLoadingGame)
        {
            // Signal to clients that the menu has been entered; used to sequence save loading.
            messageBroker.Publish(this, new MainMenuEntered());
        }
    }
}
