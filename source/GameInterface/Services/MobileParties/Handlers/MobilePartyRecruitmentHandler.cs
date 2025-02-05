using System;
using Common.Logging;
using Common.Messaging;
using Common.Network;
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
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<MobilePartyRecruitmentHandler>();

        public MobilePartyRecruitmentHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<AddNewTroop>(Handle);
            messageBroker.Subscribe<NetworkAddNewTroop>(Handle);

            messageBroker.Subscribe<AddTroopIndex>(Handle);
            messageBroker.Subscribe<NetworkAddTroopIndex>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<AddNewTroop>(Handle);
            messageBroker.Unsubscribe<AddTroopIndex>(Handle);
            messageBroker.Unsubscribe<NetworkAddNewTroop>(Handle);
            messageBroker.Unsubscribe<NetworkAddTroopIndex>(Handle);
        }
        private void Handle(MessagePayload<AddNewTroop> obj)
        {
            var payload = obj.What;

            NetworkAddNewTroop message = new NetworkAddNewTroop(
                payload.CharacterId,
                payload.PartyId,
                payload.IsPrisonRoster,
                payload.InsertAtFront,
                payload.InsertionIndex);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkAddNewTroop> obj)
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

            NetworkAddTroopIndex message = new NetworkAddTroopIndex(
                payload.PartyId,
                payload.IsPrisonerRoster,
                payload.Index,
                payload.CountChange,
                payload.WoundedCountChange,
                payload.XpChange,
                payload.RemoveDepleted);

            network.SendAll(message);
        }
        private void Handle(MessagePayload<NetworkAddTroopIndex> obj)
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