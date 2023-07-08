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

namespace Coop.Core.Client.Services.Kingdoms.Handlers
{
    /// <summary>
    /// Handles all changes to kingdoms.
    /// </summary>
    public class ClientKingdomUpdateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientKingdomUpdateHandler>();

        public ClientKingdomUpdateHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<UpdateKingdomRelation>(Handle);
            messageBroker.Subscribe<NetworkUpdateKingdomRelationRequest>(Handle);
            
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UpdateKingdomRelation>(Handle);
        }

        private void Handle(MessagePayload<UpdateKingdomRelation> obj)
        {
            var payload = obj.What;

            var message = new NetworkUpdateKingdomRelationRequest(payload.Clan.StringId, payload.Kingdom?.StringId, payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);

            network.SendAll(message);
        }
        private void Handle(MessagePayload<NetworkUpdateKingdomRelationRequest> obj)
        {
            var payload = obj.What;

            var message = new UpdatedKingdomRelation(payload.ClanId, payload.KingdomId, payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);

            messageBroker.Publish(this, message);
        }
    }
}
