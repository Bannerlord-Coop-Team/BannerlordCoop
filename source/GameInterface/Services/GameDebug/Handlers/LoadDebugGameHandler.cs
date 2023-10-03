using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Messages;

namespace GameInterface.Services.GameDebug.Handlers
{
    internal class LoadDebugGameHandler : IHandler
    {
        private readonly IDebugGameInterface gameDebugInterface;
        private readonly IMessageBroker messageBroker;

        public LoadDebugGameHandler(IDebugGameInterface gameDebugInterface, IMessageBroker messageBroker)
        {
            this.gameDebugInterface = gameDebugInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<LoadDebugGame>(Handle);

            // TODO move to different service
            messageBroker.Subscribe<LoadGame>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<LoadDebugGame>(Handle);
            messageBroker.Unsubscribe<LoadGame>(Handle);
        }

        private void Handle(MessagePayload<LoadDebugGame> payload)
        {
            messageBroker.Publish(this, new StartLoadingScreen());
            gameDebugInterface.LoadDebugGame();
        }

        private void Handle(MessagePayload<LoadGame> obj)
        {
            gameDebugInterface.LoadGame(obj.What.SaveName);
        }
    }
}
