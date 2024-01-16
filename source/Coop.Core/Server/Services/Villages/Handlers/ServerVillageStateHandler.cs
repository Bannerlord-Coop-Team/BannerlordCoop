using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.Villages.Messages;
namespace Coop.Core.Server.Services.Villages.Handlers
{
    /// <summary>
    /// TODO describe class
    /// </summary>
    internal class TemplateServerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public TemplateServerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            // This handles an internal message
            messageBroker.Subscribe<VillageStateChanged>(Handle);

        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<VillageStateChanged>(Handle);
        }

        private void Handle(MessagePayload<VillageStateChanged> obj)
        {
            var payload = obj.What;

            // Here you can add checks to allow or disallow the state change

            // Broadcast to all the clients that the state was changed
            var networkMessage = new NetworkChangeVillageState(payload.SettlementId, payload.State);
            network.SendAll(networkMessage);
        }

    }
}
