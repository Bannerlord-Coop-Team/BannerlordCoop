using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
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
            messageBroker.Subscribe<SettlementComponentChangedIsOwnerUnassigned>(IsOwnerUnassignedChanged);
            messageBroker.Subscribe<SettlementComponentChangedOwner>(OwnerChanged);
        }

        private void OwnerChanged(MessagePayload<SettlementComponentChangedOwner> payload)
        {
            if (!objectManager.TryGetObject<SettlementComponent>(payload.What.SettlementComponentId, out var obj))
            {
                Logger.Error("Unable to find SettlementComponent ({SettlementComponentId})", payload.What.SettlementComponentId);
                return;
            }
            if (!objectManager.TryGetObject<PartyBase>(payload.What.OwnerId, out var owner))
            {
                Logger.Error("Unable to find PartyBase ({OwnerId})", payload.What.OwnerId);
                return;
            }
            OwnerSettlementComponentPatch.RunSettlementComponentOwnerChanged(obj, owner);
        }

        private void IsOwnerUnassignedChanged(MessagePayload<SettlementComponentChangedIsOwnerUnassigned> payload)
        {
            if (!objectManager.TryGetObject<SettlementComponent>(payload.What.SettlementComponentId, out var obj))
            {
                Logger.Error("Unable to find SettlementComponent ({SettlementComponentId})", payload.What.SettlementComponentId);
                return;
            }
            IsOwnerUnassignedSettlementComponentPatch.RunSettlementComponentIsOwnerUnassignedChanged(obj, payload.What.IsOwnerUnassigned);
        }

        private void GoldChanged(MessagePayload<SettlementComponentChangedGold> payload)
        {
            if (!objectManager.TryGetObject<SettlementComponent>(payload.What.SettlementComponentId, out var obj))
            {
                Logger.Error("Unable to find SettlementComponent ({SettlementComponentId})", payload.What.SettlementComponentId);
                return;
            }
            GoldSettlementComponentPatch.RunSettlementComponentGoldChanged(obj, payload.What.Gold);
        }
    }
}
