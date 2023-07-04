using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.MapEvents;
using Coop.Core.Server.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.ObjectSystem;

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
            messageBroker.Subscribe<SettlementEnterAllowed>(Handle);
            messageBroker.Subscribe<SettlementLeaveAllowed>(Handle);
            messageBroker.Subscribe<PartyLeftSettlement>(Handle);
            messageBroker.Subscribe<PartyEnteredSettlement>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementEnterAllowed>(Handle);
            messageBroker.Subscribe<SettlementLeaveAllowed>(Handle);
            messageBroker.Subscribe<PartyLeftSettlement>(Handle);
            messageBroker.Unsubscribe<PartyEnteredSettlement>(Handle);
        }

        private void Handle(MessagePayload<SettlementEnterAllowed> obj)
        {
            EncounterManagerPatches.RunOriginalEnterSettlement();
        }

        private void Handle(MessagePayload<SettlementLeaveAllowed> obj)
        {
            if (objectManager.TryGetObject(obj.What.PartyId, out Settlement settlement) == false)
            {
                Logger.Error("Could not handle SettlementEnterAllowed, SettlementId not found: {id}", obj.What.StringId);
                return;
            }
            MobileParty.MainParty.CurrentSettlement = settlement;
            EncounterManagerPatches.RunOriginalLeaveSettlement();
        }

        private void Handle(MessagePayload<PartyEnteredSettlement> obj)
        {
            if (objectManager.TryGetObject(obj.What.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle SettlementEnterAllowed, PartyId not found: {id}", obj.What.PartyId);
                return;
            }

            if (objectManager.TryGetObject(obj.What.PartyId, out Settlement settlement) == false)
            {
                Logger.Error("Could not handle SettlementEnterAllowed, SettlementId not found: {id}", obj.What.StringId);
                return;
            }
            mobileParty.CurrentSettlement = settlement;
            EnterSettlementAction.ApplyForParty(mobileParty, settlement);
        }
        private void Handle(MessagePayload<PartyLeftSettlement> obj)
        {
            if (objectManager.TryGetObject(obj.What.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle SettlementEnterAllowed, PartyId not found: {id}", obj.What.PartyId);
                return;
            }

            LeaveSettlementAction.ApplyForParty(mobileParty);
        }
    }
}
