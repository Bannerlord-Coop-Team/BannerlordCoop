using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Kingdoms.Messages;
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
    /// Handles all changes to kingdoms.UpdateKingdomRelation
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
            messageBroker.Subscribe<NetworkAddPolicyRequest>(Handle);
            messageBroker.Subscribe<NetworkRemovePolicyRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkUpdateKingdomRelationRequest>(Handle);
            messageBroker.Unsubscribe<NetworkAddPolicyRequest>(Handle);
            messageBroker.Unsubscribe<NetworkRemovePolicyRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkUpdateKingdomRelationRequest> obj)
        {
            var payload = obj.What;

            var message = new KingdomRelationUpdated(payload.ClanId, payload.KingdomId, payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);

            messageBroker.Publish(this, message);

            var networkMessage = new NetworkUpdateKingdomRelationApproved(payload.ClanId, payload.KingdomId, payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);

            network.SendAll(networkMessage);

        }
        private void Handle(MessagePayload<NetworkAddPolicyRequest> obj)
        {
            var payload = obj.What;

            var message = new PolicyAdded(payload.PolicyId, payload.KingdomId);

            messageBroker.Publish(this, message);

            var networkMessage = new NetworkAddPolicyApproved(payload.PolicyId, payload.KingdomId);

            network.SendAll(networkMessage);

        }
        private void Handle(MessagePayload<NetworkRemovePolicyRequest> obj)
        {
            var payload = obj.What;

            var message = new PolicyRemoved(payload.PolicyId, payload.KingdomId);

            messageBroker.Publish(this, message);

            var networkMessage = new NetworkRemovePolicyApproved(payload.PolicyId, payload.KingdomId);

            network.SendAll(networkMessage);

        }
    }
}
