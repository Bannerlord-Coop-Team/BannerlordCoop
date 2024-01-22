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
            messageBroker.Subscribe<NetworkNewTroopRequest>(Handle);
            messageBroker.Subscribe<NewTroopAdded>(Handle); 
            messageBroker.Subscribe<NetworkTroopIndexAddRequest>(Handle);
            messageBroker.Subscribe<TroopIndexAdded>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkNewTroopRequest>(Handle);
            messageBroker.Unsubscribe<NewTroopAdded>(Handle);
            messageBroker.Unsubscribe<NetworkTroopIndexAddRequest>(Handle);
            messageBroker.Unsubscribe<TroopIndexAdded>(Handle);
        }

        internal void Handle(MessagePayload<NetworkNewTroopRequest> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddNewTroop(payload.CharacterId, payload.PartyId, payload.IsPrisonRoster, payload.InsertAtFront, payload.InsertionIndex));

            network.SendAll(new NetworkNewTroopAdded(payload.CharacterId, payload.PartyId, payload.IsPrisonRoster, payload.InsertAtFront, payload.InsertionIndex));
        }

        private void Handle(MessagePayload<NewTroopAdded> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddNewTroop(payload.CharacterId, payload.PartyId, payload.isPrisonerRoster, payload.InsertAtFront, payload.InsertionIndex));

            network.SendAll(new NetworkNewTroopAdded(payload.CharacterId, payload.PartyId, payload.isPrisonerRoster, payload.InsertAtFront, payload.InsertionIndex));
        }

        internal void Handle(MessagePayload<NetworkTroopIndexAddRequest> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddTroopIndex(payload.PartyId, payload.IsPrisonerRoster, payload.Index, payload.CountChange, payload.WoundedCountChange, payload.XpChange, payload.RemoveDepleted));

            network.SendAll(new NetworkTroopIndexAdded(payload.PartyId, payload.IsPrisonerRoster, payload.Index, payload.CountChange, payload.WoundedCountChange, payload.XpChange, payload.RemoveDepleted));
        }

        private void Handle(MessagePayload<TroopIndexAdded> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddTroopIndex(payload.PartyId, payload.IsPrisonerRoster, payload.Index, payload.CountChange, payload.WoundedCountChange, payload.XpChange, payload.RemoveDepleted));

            network.SendAll(new NetworkTroopIndexAdded(payload.PartyId, payload.IsPrisonerRoster, payload.Index, payload.CountChange, payload.WoundedCountChange, payload.XpChange, payload.RemoveDepleted));
        }
    }
}