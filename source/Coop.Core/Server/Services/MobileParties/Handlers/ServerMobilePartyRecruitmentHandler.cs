using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles all mobile party recruitment routing on server
    /// </summary>
    public class ServerMobilePartyRecruitmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerMobilePartyRecruitmentHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkRecruitRequest>(Handle);
            messageBroker.Subscribe<TroopCountChanged>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRecruitRequest>(Handle);
            messageBroker.Unsubscribe<TroopCountChanged>(Handle);
        }

        internal void Handle(MessagePayload<NetworkRecruitRequest> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new UnitRecruitGranted(payload.CharacterId, payload.Amount, payload.PartyId, payload.isPrisonRoster));

            network.SendAll(new NetworkUnitRecruited(payload.CharacterId, payload.Amount, payload.PartyId, payload.isPrisonRoster));
        }

        private void Handle(MessagePayload<TroopCountChanged> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new UnitRecruitGranted(payload.CharacterId, payload.Amount, payload.PartyId, payload.isPrisonerRoster));

            network.SendAll(new NetworkUnitRecruited(payload.CharacterId, payload.Amount, payload.PartyId, payload.isPrisonerRoster));
        }
    }
}
