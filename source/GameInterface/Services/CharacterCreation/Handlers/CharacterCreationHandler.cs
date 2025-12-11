using Common.Messaging;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Interfaces;

namespace GameInterface.Services.CharacterCreation.Handlers;

internal class CharacterCreationHandler : IHandler
{
    // Handles StartCharacterCreation by bootstrapping a new SandBox game.
    // Rationale: Character creation flow in Bannerlord is initiated by starting a new game,
    // which brings up the intro video and then the character creation stages.
    private readonly IGameStateInterface gameStateInterface;
    private readonly IMessageBroker messageBroker;

    public CharacterCreationHandler(
        IGameStateInterface gameStateInterface,
        IMessageBroker messageBroker)
    {
        this.gameStateInterface = gameStateInterface;
        this.messageBroker = messageBroker;

        // Subscribe to the StartCharacterCreation command published by client state logic.
        messageBroker.Subscribe<StartCharacterCreation>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<StartCharacterCreation>(Handle);
    }

    private void Handle(MessagePayload<StartCharacterCreation> obj)
    {
        // Start a new game on the main thread.
        // This triggers Bannerlord's built-in character creation pipeline safely.
        gameStateInterface.StartNewGame();
    }
}
