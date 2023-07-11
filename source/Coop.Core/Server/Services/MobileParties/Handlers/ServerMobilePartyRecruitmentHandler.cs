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
            messageBroker.Subscribe<PartyRecruitUnit>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRecruitRequest>(Handle);
            messageBroker.Unsubscribe<PartyRecruitUnit>(Handle);
        }

        internal void Handle(MessagePayload<NetworkRecruitRequest> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new UnitRecruitGranted(payload.CharacterId, payload.Amount, payload.PartyId));

            network.SendAllBut(obj.Who as NetPeer, new NetworkUnitRecruited(payload.CharacterId, payload.Amount, payload.PartyId));
        }

        private void Handle(MessagePayload<PartyRecruitUnit> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkPartyRecruitedUnit(
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
