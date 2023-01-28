using Common.Messaging;

namespace GameInterface.Services.CharacterCreation.Messages
{
    public readonly struct StartCharacterCreation : ICommand
    {
    }

    public readonly struct CharacterCreationFinished : IEvent
    {
    }
}
