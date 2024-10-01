using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

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

        public MobilePartyRecruitmentHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<AddNewTroop>(Handle);
            messageBroker.Subscribe<AddTroopIndex>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<AddNewTroop>(Handle);
            messageBroker.Unsubscribe<AddTroopIndex>(Handle);
        }

        private void Handle(MessagePayload<AddNewTroop> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle UnitRecruitGranted, PartyId not found: {id}", payload.PartyId);
                return;
            }

            UnitRecruitPatch.RunOriginalAddNewElement(CharacterObject.Find(payload.CharacterId), mobileParty, payload.IsPrisonRoster, payload.InsertAtFront, payload.InsertionIndex);
        }
        private void Handle(MessagePayload<AddTroopIndex> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject(payload.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle TroopIndexAddGranted, PartyId not found: {id}", payload.PartyId);
                return;
            }
            UnitRecruitPatch.RunOriginalAddToCountsAtIndex(mobileParty, payload.IsPrisonerRoster, payload.Index, payload.CountChange, payload.WoundedCountChange, payload.XpChange, payload.RemoveDepleted);
        }
    }
}
