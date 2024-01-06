using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles all mobile party recruitment in game
    /// </summary>
    public class MobilePartyRecruitmentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyRecruitmentHandler>();
        private RecruitmentCampaignBehavior recruitmentCampaignBehavior;

        public MobilePartyRecruitmentHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<UnitRecruitGranted>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UnitRecruitGranted>(Handle);
        }

        private void Handle(MessagePayload<UnitRecruitGranted> obj)
        {
            recruitmentCampaignBehavior ??= Campaign.Current.CampaignBehaviorManager.GetBehavior<RecruitmentCampaignBehavior>();

            var payload = obj.What;

            if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle UnitRecruitGranted, PartyId not found: {id}", payload.PartyId);
                return;
            }

            UnitRecruitPatch.RunOriginalAddToCounts(CharacterObject.Find(payload.CharacterId), payload.Amount, mobileParty, payload.IsPrisonRoster);
        }
    }
}