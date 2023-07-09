using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Villages.Handlers
{
    /// <summary>
    /// Handles changes to village states
    /// </summary>
    public class ClientVillageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientVillageHandler>();

        public ClientVillageHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<ChangeVillageState>(Handle);
            messageBroker.Subscribe<ChangeVillageStateApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeVillageState>(Handle);
            messageBroker.Unsubscribe<ChangeVillageStateApproved>(Handle);
        }

        private void Handle(MessagePayload<ChangeVillageStateApproved> obj)
        {
            var payload = obj.What;

            var message = new VillageStateChanged(payload.VillageId, payload.NewState, payload.PartyId);

            messageBroker.Publish(this, message);
        }

        private void Handle(MessagePayload<ChangeVillageState> obj)
        {
            var payload = obj.What;

            var message = new ChangeVillageStateRequest(payload.VillageId, payload.NewState, payload.PartyId);

            network.SendAll(message);
        }
    }
}
