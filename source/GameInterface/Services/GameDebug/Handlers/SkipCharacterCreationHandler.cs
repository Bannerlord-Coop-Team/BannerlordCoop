using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MobileParties.Messages;

namespace GameInterface.Services.GameDebug.Handlers
{
    internal class SkipCharacterCreationHandler : IHandler
    {
        private readonly IDebugCharacterCreationInterface characterCreationInterface;
        private readonly IMessageBroker messageBroker;

        public SkipCharacterCreationHandler(IDebugCharacterCreationInterface characterCreationInterface, IMessageBroker messageBroker)
        {
            this.characterCreationInterface = characterCreationInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<CharacterCreationStarted>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CharacterCreationStarted>(Handle);
        }

        private void Handle(MessagePayload<CharacterCreationStarted> obj)
        {
            characterCreationInterface.SkipCharacterCreation();
        }
    }
}
