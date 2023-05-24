using Common.Messaging;
using System;

namespace GameInterface.Services.CharacterCreation.Messages;

public record StartCharacterCreation : ICommand
{
}

public record CharacterCreationFinished : IEvent
{
}
