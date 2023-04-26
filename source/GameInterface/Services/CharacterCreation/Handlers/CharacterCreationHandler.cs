using Common.Messaging;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.GameState.Interfaces;

namespace GameInterface.Services.CharacterCreation.Handlers
{
    internal class CharacterCreationHandler : IHandler
    {
        private readonly IGameStateInterface gameStateInterface;
        private readonly IMessageBroker messageBroker;

        public CharacterCreationHandler(
            IGameStateInterface gameStateInterface,
            IMessageBroker messageBroker)
        {
            this.gameStateInterface = gameStateInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<StartCharacterCreation>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<StartCharacterCreation>(Handle);
        }

        private void Handle(MessagePayload<StartCharacterCreation> obj)
        {
            gameStateInterface.StartNewGame();
        }
    }
}
