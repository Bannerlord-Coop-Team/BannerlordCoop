using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers
{
    public class MobilePartyRecruitmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyRecruitmentHandler>();

        public MobilePartyRecruitmentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;

            messageBroker.Subscribe<UnitRecruitGranted>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UnitRecruitGranted>(Handle);
        }

        internal void Handle(MessagePayload<UnitRecruitGranted> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle SettlementEnterAllowed, PartyId not found: {id}", payload.PartyId);
                return;
            }

            mobileParty.AddElementToMemberRoster(CharacterObject.Find(payload.CharacterId), payload.Amount);
        }
    }
}
