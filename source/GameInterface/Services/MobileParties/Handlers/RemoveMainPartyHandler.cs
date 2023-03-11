using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Interfaces;

namespace GameInterface.Services.MobileParties.Handlers
{
    internal class RemoveMainPartyHandler : IHandler
    {
        private readonly IMainPartyInterface mainPartyInterface;
        private readonly IMessageBroker messageBroker;

        public RemoveMainPartyHandler(IMainPartyInterface mainPartyInterface, IMessageBroker messageBroker)
        {
            this.mainPartyInterface = mainPartyInterface;
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<RemoveMainParty>(Handle);
        }

        private void Handle(MessagePayload<RemoveMainParty> obj)
        {
            mainPartyInterface.RemoveMainParty();

            messageBroker.Publish(this, new MainPartyRemoved());
        }
    }
}
