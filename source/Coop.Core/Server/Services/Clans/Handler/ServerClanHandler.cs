using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Server.Services.Clans.Messages;
using GameInterface.Services.Clans.Messages;
using Serilog;
using System;

namespace Coop.Core.Server.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans on server.
    /// </summary>
    public class ServerClanHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanHandler>();

        public ServerClanHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkClanNameChangeRequest>(Handle);

            messageBroker.Subscribe<NetworkClanLeaderChangeRequest>(Handle);

            messageBroker.Subscribe<NetworkClanKingdomChangeRequest>(Handle);

            messageBroker.Subscribe<NetworkDestroyClanRequest>(Handle);

            messageBroker.Subscribe<NetworkAddCompanionRequest>(Handle);

            messageBroker.Subscribe<NetworkAddRenownRequest>(Handle);

            messageBroker.Subscribe<NetworkChangeClanInfluenceRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkClanNameChangeRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkClanNameChangeRequest> obj)
        {
            var payload = obj.What;

            ClanNameChanged clanNameChanged = new ClanNameChanged(payload.ClanId, payload.Name, payload.InformalName);

            messageBroker.Publish(this, clanNameChanged);

            NetworkClanNameChangeApproved clanNameChangeApproved = new NetworkClanNameChangeApproved(payload.ClanId, payload.Name, payload.InformalName);

            network.SendAll(clanNameChangeApproved);
        }

        private void Handle(MessagePayload<NetworkClanLeaderChangeRequest> obj)
        {
            var payload = obj.What;

            ClanLeaderChanged clanLeaderChanged = new ClanLeaderChanged(payload.ClanId, payload.NewLeaderId);

            messageBroker.Publish(this, clanLeaderChanged);

            NetworkClanLeaderChangeApproved clanLeaderChangeApproved = new NetworkClanLeaderChangeApproved(payload.ClanId, payload.NewLeaderId);

            network.SendAll(clanLeaderChangeApproved);
        }

        private void Handle(MessagePayload<NetworkClanKingdomChangeRequest> obj)
        {
            var payload = obj.What;

            ClanKingdomChanged clanKingdomChanged = new ClanKingdomChanged(payload.ClanId, payload.NewKingdomId, payload.DetailId, 
                payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification);

            messageBroker.Publish(this, clanKingdomChanged);

            NetworkClanKingdomChangeApproved clanKingdomChangeApproved = new NetworkClanKingdomChangeApproved(payload.ClanId, payload.NewKingdomId,
                payload.DetailId, payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification);

            network.SendAll(clanKingdomChangeApproved);
        }

        private void Handle(MessagePayload<NetworkDestroyClanRequest> obj)
        {
            var payload = obj.What;

            ClanDestroyed clanDestroyed = new ClanDestroyed(payload.ClanId, payload.DetailId);

            messageBroker.Publish(this, clanDestroyed);

            NetworkDestroyClanApproved destroyClanApproved = new NetworkDestroyClanApproved(payload.ClanId, payload.DetailId);

            network.SendAll(destroyClanApproved);
        }

        private void Handle(MessagePayload<NetworkAddCompanionRequest> obj)
        {
            var payload = obj.What;

            CompanionAdded companionAdded = new CompanionAdded(payload.ClanId, payload.CompanionId);

            messageBroker.Publish(this, companionAdded);

            NetworkCompanionAddApproved companionAddApproved = new NetworkCompanionAddApproved(payload.ClanId, payload.CompanionId);

            network.SendAll(companionAddApproved);
        }

        private void Handle(MessagePayload<NetworkAddRenownRequest> obj)
        {
            var payload = obj.What;

            RenownAdded renownAdded = new RenownAdded(payload.ClanId, payload.Amount, payload.ShouldNotify);

            messageBroker.Publish(this, renownAdded);

            NetworkRenownAddApproved renownAddApproved = new NetworkRenownAddApproved(payload.ClanId, payload.Amount, payload.ShouldNotify);

            network.SendAll(renownAddApproved);
        }
        private void Handle(MessagePayload<NetworkChangeClanInfluenceRequest> obj)
        {
            var payload = obj.What;

            ClanInfluenceChanged clanInfluenceChanged = new ClanInfluenceChanged(payload.ClanId, payload.Amount);

            messageBroker.Publish(this, clanInfluenceChanged);

            NetworkClanChangeInfluenceApproved clanChangeInfluenceApproved = new NetworkClanChangeInfluenceApproved(payload.ClanId, payload.Amount);

            network.SendAll(clanChangeInfluenceApproved);
        }
    }
}