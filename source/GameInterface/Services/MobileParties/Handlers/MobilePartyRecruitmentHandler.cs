using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Handlers
{
    public class MobilePartyRecruitmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyRecruitmentHandler>();
        private RecruitmentCampaignBehavior recruitmentCampaignBehavior;

        public MobilePartyRecruitmentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;

            messageBroker.Subscribe<UnitRecruitGranted>(Handle);
            messageBroker.Subscribe<PartyRecruitGranted>(Handle);

            
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UnitRecruitGranted>(Handle);
        }

        //A Player Recruited Unit
        internal void Handle(MessagePayload<UnitRecruitGranted> obj)
        {
            recruitmentCampaignBehavior ??= Campaign.Current.CampaignBehaviorManager.GetBehavior<RecruitmentCampaignBehavior>();

            var payload = obj.What;

            if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle SettlementEnterAllowed, PartyId not found: {id}", payload.PartyId);
                return;
            }

            mobileParty.AddElementToMemberRoster(CharacterObject.Find(payload.CharacterId), payload.Amount);
        }

        private static readonly MethodInfo recruit_ApplyInternal = typeof(RecruitmentCampaignBehavior).GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Instance)!;
        
        //NPC Recruited Unit
        private void Handle(MessagePayload<PartyRecruitGranted> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle PartyRecruitGranted, PartyId not found: {id}", payload.PartyId);
                return;
            }
            if (objectManager.TryGetObject(payload.SettlementId, out Settlement settlement) == false && payload.SettlementId != null)
            {
                Logger.Error("Could not handle PartyRecruitGranted, Settlement not found: {id}", payload.SettlementId);
                return;
            }
            if (objectManager.TryGetObject(payload.HeroId, out Hero hero) == false && payload.HeroId != null)
            {
                Logger.Error("Could not handle PartyRecruitGranted, HeroId not found: {id}", payload.HeroId);
                return;
            }

            recruit_ApplyInternal.Invoke(recruitmentCampaignBehavior, new object[]
            {
                mobileParty,
                settlement,
                hero,
                CharacterObject.Find(payload.CharacterId),
                payload.Amount,
                payload.BitCode,
                payload.RecruitingDetail
            });
        }
    }
}
