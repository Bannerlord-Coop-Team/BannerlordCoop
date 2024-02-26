using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Handlers
{
    public class SettlementComponentHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<SettlementHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public SettlementComponentHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<SettlementComponentChangedGold>(GoldChanged);
        }

        private void GoldChanged(MessagePayload<SettlementComponentChangedGold> payload)
        {
            if (!objectManager.TryGetObject<SettlementComponent>(payload.What.SettlementComponentId, out var obj))
            {
                Logger.Error("Unable to find SettlementComponent ({SettlementComponentId})", obj.StringId);
                return;
            }
            GoldSettlementComponentPatch.RunSettlementComponentGoldChanged(obj, payload.What.Gold);
        }
    }
}
