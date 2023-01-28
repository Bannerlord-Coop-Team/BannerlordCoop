using Common.Messaging;
using GameInterface.Services.CharacterCreation.Interfaces;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.UI.Messages;

namespace GameInterface.Services.CharacterCreation.Handlers
{
    internal class CharacterCreationHandler : IHandler
    {
        private readonly ICharacterCreationInterface characterCreationInterface;
        private readonly IMessageBroker messageBroker;

        public CharacterCreationHandler(
            ICharacterCreationInterface characterCreationInterface,
            IMessageBroker messageBroker)
        {
            this.characterCreationInterface = characterCreationInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<StartCharacterCreation>(Handle);
        }

        private void Handle(MessagePayload<StartCharacterCreation> obj)
        {
            characterCreationInterface.StartCharacterCreation();
        }
    }
}
