using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using Serilog;

namespace Coop.Core.Server.Services.Villages.Handlers
{
    /// <summary>
    /// Handles changes to village states
    /// </summary>
    public class ServerVillageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ServerVillageHandler>();

        public ServerVillageHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ChangeVillageStateRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeVillageStateRequest>(Handle);
        }

        private void Handle(MessagePayload<ChangeVillageStateRequest> obj)
        {
            var payload = obj.What;

            var message = new VillageStateChanged(payload.VillageId, payload.NewState, payload.PartyId);

            messageBroker.Publish(this, message);

            var networkMessage = new ChangeVillageStateApproved(payload.VillageId, payload.NewState, payload.PartyId);

            network.SendAll(networkMessage);
        }
    }
}
