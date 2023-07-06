using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

namespace GameInterface.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class SettlementExitEnterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<SettlementExitEnterHandler>();

        public SettlementExitEnterHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<PartySettlementEnter>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PartySettlementEnter>(Handle);
        }

        private void Handle(MessagePayload<PartySettlementEnter> obj)
        {
            if (objectManager.TryGetObject(obj.What.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle PartyEnteredSettlement, PartyId not found: {id}", obj.What.PartyId);
                return;
            }

            if (objectManager.TryGetObject(obj.What.SettlementId, out Settlement settlement) == false)
            {
                Logger.Error("Could not handle PartyEnteredSettlement, SettlementId not found: {id}", obj.What.SettlementId);
                return;
            }

            EnterSettlementActionPatches.OverrideApplyForParty(mobileParty, settlement);
        }
    }
}