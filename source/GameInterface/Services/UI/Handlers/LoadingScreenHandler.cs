using Common.Messaging;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Messages;
using TaleWorlds.Engine;

namespace GameInterface.Services.GameDebug.Interfaces
{
    internal class LoadingScreenHandler : IHandler
    {
        private readonly IUIInterface UIInterface;
        private readonly IMessageBroker messageBroker;

        public LoadingScreenHandler(IUIInterface UIInterface, IMessageBroker messageBroker)
        {
            this.UIInterface = UIInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<StartLoadingScreen>(Handle);
            messageBroker.Subscribe<EndLoadingScreen>(Handle);
        }

        private void Handle(MessagePayload<StartLoadingScreen> obj)
        {
            LoadingWindow.EnableGlobalLoadingWindow();
        }

        private void Handle(MessagePayload<EndLoadingScreen> obj)
        {
            LoadingWindow.DisableGlobalLoadingWindow();
        }
    }
}
