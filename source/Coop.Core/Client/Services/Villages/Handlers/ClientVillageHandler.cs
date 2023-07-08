using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Client.Services.Villages.Handlers
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class ClientVillageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ClientVillageHandler>();

        public ClientVillageHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ChangeVillageState>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeVillageState>(Handle);
        }

        private void Handle(MessagePayload<ChangeVillageState> obj)
        {
            var payload = obj.What;

            var message = new VillageStateChanged(payload.VillageId, payload.NewState, payload.PartyId);

            messageBroker.Publish(this, message);
        }
    }
}
