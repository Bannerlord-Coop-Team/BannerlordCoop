using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;

namespace GameInterface.Services.MobileParties.Handlers
{
    public class PlayerEscapeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;

        public PlayerEscapeHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<EscapePlayer>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<EscapePlayer>(Handle);
        }

        private void Handle(MessagePayload<EscapePlayer> obj)
        {
            PlayerEscapePatch.RunEscapeCaptivityMenu();
        }
    }
}