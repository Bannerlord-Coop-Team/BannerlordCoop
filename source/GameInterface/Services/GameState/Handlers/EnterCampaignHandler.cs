using Common;
using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace GameInterface.Services.GameState.Handlers
{
    internal class EnterCampaignHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;

        public EnterCampaignHandler(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;

            messageBroker.Subscribe<EnterCampaignState>(Handle);
        }

        private void Handle(MessagePayload<EnterCampaignState> obj)
        {
            messageBroker.Publish(this, new CampaignStateEntered());
        }
    }
}
