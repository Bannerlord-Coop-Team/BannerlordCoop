using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace Coop.Core.Server.Services.Kingdoms.Handlers
{
    /// <summary>
    /// Handles all changes to kingdoms.
    /// </summary>
    public class ServerKingdomUpdateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ServerKingdomUpdateHandler>();

        public ServerKingdomUpdateHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<NetworkUpdateKingdomRelationRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkUpdateKingdomRelationRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkUpdateKingdomRelationRequest> obj)
        {
            var payload = obj.What;

            var message = new UpdatedKingdomRelation(payload.ClanId, payload.KingdomId, payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);

            messageBroker.Publish(this, message);

            var networkMessage = new NetworkUpdateKingdomRelationRequest(payload.ClanId, payload.KingdomId, payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);

            network.SendAll(networkMessage);

        }
    }
}
