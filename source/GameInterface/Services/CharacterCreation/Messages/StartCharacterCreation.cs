using Common.Messaging;

namespace GameInterface.Services.CharacterCreation.Messages;

public record StartCharacterCreation : ICommand
{
}

public record CharacterCreationFinished : IEvent
{
}
