using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.Settlements.Handlers
{
    /// <summary>
    /// Handles all <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent"/> changes
    /// </summary>
    public class ServerSettlementComponentHandler : IHandler
    {
        private readonly ILogger logger = LogManager.GetLogger<ServerSettlementComponentHandler>();

        private IMessageBroker messageBroker;
        private INetwork network;
        private readonly IObjectManager objectManager;

        public ServerSettlementComponentHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<SettlementComponentGoldChanged>(ChangedGold);
            messageBroker.Subscribe<SettlementComponentIsOwnerUnassignedChanged>(ChangedIsOwnerUnassigned);
            messageBroker.Subscribe<SettlementComponentOwnerChanged>(ChangedOwner);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementComponentGoldChanged>(ChangedGold);
            messageBroker.Unsubscribe<SettlementComponentIsOwnerUnassignedChanged>(ChangedIsOwnerUnassigned);
            messageBroker.Unsubscribe<SettlementComponentOwnerChanged>(ChangedOwner);
        }

        private void ChangedOwner(MessagePayload<SettlementComponentOwnerChanged> payload)
        {
            var obj = payload.What;

            if (!objectManager.TryGetId(obj.SettlementComponent, out var settlementComponentId))
            {
                logger.Error("Could not find id for {type}", typeof(SettlementComponent));
                return;
            }

            network.SendAll(new NetworkChangeSettlementComponentOwner(settlementComponentId, obj.OwnerId));
        }

        private void ChangedIsOwnerUnassigned(MessagePayload<SettlementComponentIsOwnerUnassignedChanged> payload)
        {
            var obj = payload.What;

            if (!objectManager.TryGetId(obj.SettlementComponent, out var settlementComponentId))
            {
                logger.Error("Could not find id for {type}", typeof(SettlementComponent));
                return;
            }

            network.SendAll(new NetworkChangeSettlementComponentIsOwnerUnassigned(settlementComponentId, obj.IsOwnerUnassigned));
        }

        private void ChangedGold(MessagePayload<SettlementComponentGoldChanged> payload)
        {
            var obj = payload.What;

            if (!objectManager.TryGetId(obj.SettlementComponent, out var settlementComponentId))
            {
                logger.Error("Could not find id for {type}", typeof(SettlementComponent));
                return;
            }

            network.SendAll(new NetworkChangeSettlementComponentGold(settlementComponentId, obj.Gold));
        }

    }
}
