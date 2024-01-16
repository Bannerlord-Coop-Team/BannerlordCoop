using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Clans.Handler;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.Template.Messages;
using GameInterface.Services.Villages.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.Villages.Handlers
{
    /// <summary>
    /// TODO describe class
    /// </summary>
    internal class ServerVillageStateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerVillageStateHandler>();

        public ServerVillageStateHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            // This handles an internal message
            messageBroker.Subscribe<ClientVillageStateChange>(Handle);


        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClientVillageStateChange>(Handle);
        }

        private void Handle(MessagePayload<ClientVillageStateChange> obj)
        {
            var payload = obj.What.VillageChanged;


            network.SendAll(new ServerVillageChangeState(payload));
        }
    }
}
