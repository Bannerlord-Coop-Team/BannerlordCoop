using Common.Messaging;
using System;

namespace GameInterface.Services.CharacterCreation.Messages
{
    public readonly struct StartCharacterCreation : ICommand
    {
        public Guid TransactionID => throw new NotImplementedException();
    }

    public readonly struct CharacterCreationFinished : IResponse
    {
        public Guid TransactionID => throw new NotImplementedException();
    }
}
