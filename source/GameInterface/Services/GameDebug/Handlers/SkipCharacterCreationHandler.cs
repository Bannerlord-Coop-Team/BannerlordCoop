using Common;
using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.GameDebug.Handlers
{
    internal class SkipCharacterCreationHandler
    {
        private readonly ICharacterCreationInterface characterCreationInterface;
        private readonly IMessageBroker messageBroker;

        public SkipCharacterCreationHandler(ICharacterCreationInterface characterCreationInterface, IMessageBroker messageBroker)
        {
            this.characterCreationInterface = characterCreationInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<CharacterCreationStarted>(Handle);
            messageBroker.Subscribe<GameLoaded>(Handle);
        }

        private void Handle(MessagePayload<GameLoaded> obj)
        {
            messageBroker.Publish(this, new CharacterCreationFinished());
        }

        private void Handle(MessagePayload<CharacterCreationStarted> obj)
        {
            characterCreationInterface.SkipCharacterCreation();
        }
    }
}
