using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.GameState.Handlers
{
    internal class EnterMainMenuHandler : IEnterMainMenuHandler
    {
        private readonly IGameStateInterface gameStateInterface;
        private readonly IMessageBroker messageBroker;

        public EnterMainMenuHandler(IGameStateInterface gameStateInterface, IMessageBroker messageBroker)
        {
            this.gameStateInterface = gameStateInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<EnterMainMenuCommand>(Handle);
        }

        private void Handle(MessagePayload<EnterMainMenuCommand> payload)
        {
            gameStateInterface.EnterMainMenu();

            messageBroker.Publish(this, new MainMenuEntered());
        }
    }
}
