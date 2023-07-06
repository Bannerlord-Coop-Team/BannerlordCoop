using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MapEvents.Handlers
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class SettlementExitEnterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<SettlementExitEnterHandler>();

        public SettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<PartyEnteredSettlement>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PartyEnteredSettlement>(Handle);
        }

        private void Handle(MessagePayload<PartyEnteredSettlement> obj)
        {
            if (objectManager.TryGetObject(obj.What.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle PartyEnteredSettlement, PartyId not found: {id}", obj.What.PartyId);
                return;
            }

            if (objectManager.TryGetObject(obj.What.StringId, out Settlement settlement) == false)
            {
                Logger.Error("Could not handle PartyEnteredSettlement, SettlementId not found: {id}", obj.What.StringId);
                return;
            }

            EnterSettlementAction.ApplyForParty(mobileParty, settlement);
        }
    }
}