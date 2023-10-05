using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using Serilog;
using GameInterface.Services.Clans.Messages;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Server.Services.Clans.Messages;
using System;

namespace Coop.Core.Client.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans.
    /// </summary>
    public class ClientClanHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanHandler>();

        public ClientClanHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<ClanNameChange>(Handle);
            messageBroker.Subscribe<NetworkClanNameChangeApproved>(Handle);
            

            messageBroker.Subscribe<ClanLeaderChange>(Handle);
            messageBroker.Subscribe<NetworkClanLeaderChangeApproved>(Handle);

            messageBroker.Subscribe<ClanKingdomChange>(Handle);
            messageBroker.Subscribe<NetworkClanKingdomChangeApproved>(Handle);

            messageBroker.Subscribe<DestroyClan>(Handle);
            messageBroker.Subscribe<NetworkDestroyClanApproved>(Handle);

            messageBroker.Subscribe<AddCompanion>(Handle);
            messageBroker.Subscribe<NetworkCompanionAddApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChange>(Handle);
            messageBroker.Unsubscribe<NetworkClanNameChangeApproved>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChange> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanNameChangeRequest(payload.Clan.StringId, payload.Name, payload.InformalName));
        }

        private void Handle(MessagePayload<NetworkClanNameChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanNameChanged(payload.ClanId, payload.Name, payload.InformalName));

        }

        private void Handle(MessagePayload<ClanLeaderChange> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanLeaderChangeRequest(payload.Clan.StringId, payload.NewLeader?.StringId));
        }

        private void Handle(MessagePayload<NetworkClanLeaderChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanLeaderChanged(payload.ClanId, payload.NewLeaderId));
        }

        private void Handle(MessagePayload<ClanKingdomChange> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanKingdomChangeRequest(payload.Clan.StringId, payload.NewKingdom?.StringId, 
                (int)payload.Detail, payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification));
        }

        private void Handle(MessagePayload<NetworkClanKingdomChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanKingdomChanged(payload.ClanId, payload.NewKingdomId, payload.DetailId, 
                payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification));
        }

        private void Handle(MessagePayload<DestroyClan> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkDestroyClanRequest(payload.Clan.StringId, payload.Details));
        }

        private void Handle(MessagePayload<NetworkDestroyClanApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanDestroyed(payload.ClanId, payload.DetailId));
        }

        private void Handle(MessagePayload<AddCompanion> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkAddCompanionRequest(payload.Clan.StringId, payload.Companion.StringId));
        }

        private void Handle(MessagePayload<NetworkCompanionAddApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new CompanionAdded(payload.ClanId, payload.CompanionId));
        }
    }
}
