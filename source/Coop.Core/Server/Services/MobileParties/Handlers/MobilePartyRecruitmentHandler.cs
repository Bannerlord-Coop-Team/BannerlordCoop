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
    public class MobilePartyRecruitmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public MobilePartyRecruitmentHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkRecruitRequest>(Handle);
            messageBroker.Subscribe<NetworkPartyRecruitUnit>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRecruitRequest>(Handle);
            messageBroker.Unsubscribe<NetworkPartyRecruitUnit>(Handle);
        }

        internal void Handle(MessagePayload<NetworkRecruitRequest> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new UnitRecruitGranted(payload.CharacterId, payload.Amount, payload.PartyId));

            network.SendAllBut(obj.Who as NetPeer, new NetworkUnitRecruited(payload.CharacterId, payload.Amount, payload.PartyId));
        }

        private void Handle(MessagePayload<NetworkPartyRecruitUnit> obj)
        {
            var payload = obj.What;

            network.SendAll(new PartyRecruitedUnit(
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
