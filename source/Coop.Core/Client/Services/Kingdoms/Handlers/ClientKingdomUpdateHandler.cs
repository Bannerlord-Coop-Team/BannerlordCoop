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
            messageBroker.Subscribe<NetworkUpdateKingdomRelationApproved>(Handle);
            messageBroker.Subscribe<AddPolicy>(Handle);
            messageBroker.Subscribe<RemovePolicy>(Handle);
            messageBroker.Subscribe<NetworkAddPolicyApproved>(Handle);
            messageBroker.Subscribe<NetworkRemovePolicyApproved>(Handle);

        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UpdateKingdomRelation>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateKingdomRelationApproved>(Handle);
            messageBroker.Unsubscribe<AddPolicy>(Handle);
            messageBroker.Unsubscribe<RemovePolicy>(Handle);
            messageBroker.Unsubscribe<NetworkAddPolicyApproved>(Handle);
            messageBroker.Unsubscribe<NetworkRemovePolicyApproved>(Handle);
        }

        private void Handle(MessagePayload<UpdateKingdomRelation> obj)
        {
            var payload = obj.What;

            var message = new NetworkUpdateKingdomRelationRequest(payload.Clan.StringId, payload.Kingdom?.StringId, payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);

            network.SendAll(message);
        }
        private void Handle(MessagePayload<NetworkUpdateKingdomRelationApproved> obj)
        {
            var payload = obj.What;

            var message = new UpdatedKingdomRelation(payload.ClanId, payload.KingdomId, payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);

            messageBroker.Publish(this, message);
        }

        private void Handle(MessagePayload<AddPolicy> obj)
        {
            var payload = obj.What;

            var message = new NetworkAddPolicyRequest(payload.PolicyId, payload.KingdomId);

            network.SendAll(message);
        }
        private void Handle(MessagePayload<NetworkAddPolicyApproved> obj)
        {
            var payload = obj.What;

            var message = new PolicyAdded(payload.PolicyId, payload.KingdomId);

            messageBroker.Publish(this, message);
        }
        private void Handle(MessagePayload<RemovePolicy> obj)
        {
            var payload = obj.What;

            var message = new NetworkRemovePolicyRequest(payload.PolicyId, payload.KingdomId);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkRemovePolicyApproved> obj)
        {
            var payload = obj.What;

            var message = new PolicyRemoved(payload.PolicyId, payload.KingdomId);

            messageBroker.Publish(this, message);
        }
    }
}