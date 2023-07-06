using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using GameInterface.Services.Clans.Messages;
using Coop.Core.Client.Services.Clans.Messages;
using TaleWorlds.CampaignSystem;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Party;
using Coop.Core.Server.Services.Clans.Messages;

namespace Coop.Core.Client.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans.
    /// </summary>
    public class ClientClanHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanHandler>();

        public ClientClanHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ClanNameChange>(Handle);
            messageBroker.Subscribe<ClanLeaveKingdom>(Handle);
            messageBroker.Subscribe<NetworkClanLeaveKingdomApproved>(Handle);
            messageBroker.Subscribe<NetworkClanNameChangeApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChange>(Handle);
            messageBroker.Unsubscribe<ClanLeaveKingdom>(Handle);
            messageBroker.Unsubscribe<NetworkClanLeaveKingdomApproved>(Handle);
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

        private void Handle(MessagePayload<ClanLeaveKingdom> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanLeaveKingdomRequest(payload.Clan.StringId, payload.GiveBackFiefs));
        }

        private void Handle(MessagePayload<NetworkClanLeaveKingdomApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanLeftKingdom(payload.ClanId, payload.GiveBackFiefs));

        }
    }
}
