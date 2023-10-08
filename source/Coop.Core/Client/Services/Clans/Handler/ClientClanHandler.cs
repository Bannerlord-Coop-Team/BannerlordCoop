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

            messageBroker.Subscribe<AddRenown>(Handle);
            messageBroker.Subscribe<NetworkRenownAddApproved>(Handle);

            messageBroker.Subscribe<ChangeClanInfluence>(Handle);
            messageBroker.Subscribe<NetworkClanChangeInfluenceApproved>(Handle);

            messageBroker.Subscribe<AdoptHero>(Handle);
            messageBroker.Subscribe<NetworkAdoptHeroApproved>(Handle);

            messageBroker.Subscribe<LocalNewHeir>(Handle);
            messageBroker.Subscribe<NetworkNewHeirApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChange>(Handle);
            messageBroker.Unsubscribe<NetworkClanNameChangeApproved>(Handle);
            messageBroker.Unsubscribe<ClanLeaderChange>(Handle);
            messageBroker.Unsubscribe<NetworkClanLeaderChangeApproved>(Handle);

            messageBroker.Unsubscribe<ClanKingdomChange>(Handle);
            messageBroker.Unsubscribe<NetworkClanKingdomChangeApproved>(Handle);

            messageBroker.Unsubscribe<DestroyClan>(Handle);
            messageBroker.Unsubscribe<NetworkDestroyClanApproved>(Handle);

            messageBroker.Unsubscribe<AddCompanion>(Handle);
            messageBroker.Unsubscribe<NetworkCompanionAddApproved>(Handle);

            messageBroker.Unsubscribe<AddRenown>(Handle);
            messageBroker.Unsubscribe<NetworkRenownAddApproved>(Handle);

            messageBroker.Unsubscribe<ChangeClanInfluence>(Handle);
            messageBroker.Unsubscribe<NetworkClanChangeInfluenceApproved>(Handle);

            messageBroker.Unsubscribe<AdoptHero>(Handle);
            messageBroker.Unsubscribe<NetworkAdoptHeroApproved>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChange> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanNameChangeRequest(payload.ClanId, payload.Name, payload.InformalName));
        }

        private void Handle(MessagePayload<NetworkClanNameChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanNameChanged(payload.ClanId, payload.Name, payload.InformalName));

        }

        private void Handle(MessagePayload<ClanLeaderChange> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanLeaderChangeRequest(payload.ClanId, payload.NewLeaderId));
        }

        private void Handle(MessagePayload<NetworkClanLeaderChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanLeaderChanged(payload.ClanId, payload.NewLeaderId));
        }

        private void Handle(MessagePayload<ClanKingdomChange> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanKingdomChangeRequest(payload.ClanId, payload.NewKingdomId, 
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

            network.SendAll(new NetworkDestroyClanRequest(payload.ClanId, payload.Details));
        }

        private void Handle(MessagePayload<NetworkDestroyClanApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanDestroyed(payload.ClanId, payload.DetailId));
        }

        private void Handle(MessagePayload<AddCompanion> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkAddCompanionRequest(payload.ClanId, payload.CompanionId));
        }

        private void Handle(MessagePayload<NetworkCompanionAddApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new CompanionAdded(payload.ClanId, payload.CompanionId));
        }

        private void Handle(MessagePayload<AddRenown> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkAddRenownRequest(payload.ClanId, payload.Amount, payload.ShouldNotify));
        }
        private void Handle(MessagePayload<NetworkRenownAddApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new RenownAdded(payload.ClanId, payload.Amount, payload.ShouldNotify));
        }

        private void Handle(MessagePayload<ChangeClanInfluence> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkChangeClanInfluenceRequest(payload.ClanId, payload.Amount));
        }

        private void Handle(MessagePayload<NetworkClanChangeInfluenceApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanInfluenceChanged(payload.ClanId, payload.Amount));
        }
        private void Handle(MessagePayload<AdoptHero> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkAdoptHeroRequest(payload.AdoptedHeroId, payload.ClanId, payload.PlayerHeroId));
        }

        private void Handle(MessagePayload<NetworkAdoptHeroApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new HeroAdopted(payload.HeroId, payload.PlayerClanId, payload.PlayerHeroId));
        }
        private void Handle(MessagePayload<LocalNewHeir> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkNewHeirRequest(payload.HeirHeroId, payload.PlayerHeroId, payload.IsRetirement));
        }
        private void Handle(MessagePayload<NetworkNewHeirApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new NewHeirAppointed(payload.HeirHeroId, payload.PlayerHeroId, payload.IsRetirement));
        }
    }
}