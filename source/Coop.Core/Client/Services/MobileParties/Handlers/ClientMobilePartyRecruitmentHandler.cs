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
            messageBroker.Subscribe<OnUnitRecruited>(Handle);
            messageBroker.Subscribe<NetworkUnitRecruited>(Handle);
            messageBroker.Subscribe<PartyRecruitedUnit>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<OnUnitRecruited>(Handle);
            messageBroker.Unsubscribe<NetworkUnitRecruited>(Handle);
            messageBroker.Unsubscribe<PartyRecruitedUnit>(Handle);
        }

        private void Handle(MessagePayload<NetworkUnitRecruited> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new UnitRecruitGranted(payload.CharacterId, payload.Amount, payload.PartyId));
        }

        internal void Handle(MessagePayload<OnUnitRecruited> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkRecruitRequest(payload.CharacterId, payload.Amount, payload.PartyId));
        }

        private void Handle(MessagePayload<PartyRecruitedUnit> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new PartyRecruitGranted(
                payload.PartyId,
                payload.SettlementId,
                payload.HeroId,
                payload.CharacterId,
                payload.Amount,
                payload.BitCode,
                payload.RecruitingDetail
                ));
        }
    }
}
