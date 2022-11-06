using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.GameState.Handlers
{
    internal class EnterMissionHandler : IHandler
    {
        private readonly IGameStateInterface gameStateInterface;
        private readonly IMessageBroker messageBroker;

        public EnterMissionHandler(IGameStateInterface gameStateInterface, IMessageBroker messageBroker)
        {
            this.gameStateInterface = gameStateInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<EnterMainMenu>(Handle);
        }

        private void Handle(MessagePayload<EnterMainMenu> obj)
        {
            messageBroker.Publish(this, new EnterMissionState());
        }
    }
}
