using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles all mobile party recruitment routing on client
    /// </summary>
    public class ClientMobilePartyRecruitmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientMobilePartyRecruitmentHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NewTroopAdded>(Handle);
            messageBroker.Subscribe<NetworkAddNewTroop>(Handle);
            messageBroker.Subscribe<TroopIndexAdded>(Handle);
            messageBroker.Subscribe<NetworkAddTroopIndex>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<NewTroopAdded>(Handle);
            messageBroker.Unsubscribe<NetworkAddNewTroop>(Handle);
            messageBroker.Unsubscribe<TroopIndexAdded>(Handle);
            messageBroker.Unsubscribe<NetworkAddTroopIndex>(Handle);
        }

        internal void Handle(MessagePayload<NewTroopAdded> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkNewTroopRequest(payload.CharacterId, payload.PartyId, payload.isPrisonerRoster, payload.InsertAtFront, payload.InsertionIndex));
        }
        internal void Handle(MessagePayload<NetworkAddNewTroop> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddNewTroop(payload.CharacterId, payload.PartyId, payload.IsPrisonerRoster, payload.InsertAtFront, payload.InsertionIndex));
        }

        internal void Handle(MessagePayload<TroopIndexAdded> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkTroopIndexAddRequest(payload.PartyId, payload.IsPrisonerRoster, payload.Index, payload.CountChange, payload.WoundedCountChange, payload.XpChange, payload.RemoveDepleted));
        }
        internal void Handle(MessagePayload<NetworkAddTroopIndex> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddTroopIndex(payload.PartyId, payload.IsPrisonerRoster, payload.Index, payload.CountChange, payload.WoundedCountChange, payload.XpChange, payload.RemoveDepleted));
        }
    }
}