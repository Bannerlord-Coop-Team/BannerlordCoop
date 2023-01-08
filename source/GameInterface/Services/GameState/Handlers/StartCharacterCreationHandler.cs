using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.GameState.Handlers
{
    internal class StartCharacterCreationHandler : IHandler
    {
        private readonly IGameStateInterface gameStateInterface;
        private readonly IMessageBroker messageBroker;

        public StartCharacterCreationHandler(IGameStateInterface gameStateInterface, IMessageBroker messageBroker)
        {
            this.gameStateInterface = gameStateInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<GameLoaded>(Handle);
        }

        private void Handle(MessagePayload<GameLoaded> payload)
        {
            
        }
    }
}
