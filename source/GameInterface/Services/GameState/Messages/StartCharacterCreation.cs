using Common.Messaging;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct StartCreateCharacter : ICommand
    {
    }

    public readonly struct CharacterCreationFinished : IEvent
    {
    }
}
