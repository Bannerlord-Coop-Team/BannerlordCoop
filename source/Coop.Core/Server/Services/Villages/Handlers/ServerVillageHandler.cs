using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.Villages.Messages;
using System;
using System.Runtime.InteropServices;
namespace Coop.Core.Server.Services.Villages.Handlers
{
    /// <summary>
    /// Handles VillageStates changes on the server.
    /// </summary>
    internal class ServerVillageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerVillageHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            // This handles an internal message
            messageBroker.Subscribe<VillageStateChanged>(HandleVillageState);
            messageBroker.Subscribe<VillageTradeBoundChanged>(HandleTradeBound);
            messageBroker.Subscribe<VillageHearthChanged>(HandleHearth);
            messageBroker.Subscribe<VillageTaxAccumulateChanged>(HandleTradeTaxAccumulated);
            messageBroker.Subscribe<VillageDemandTimeChanged>(HandleLastDemandSatisifiedTime);

        }

        private void HandleLastDemandSatisifiedTime(MessagePayload<VillageDemandTimeChanged> payload)
        {
            var obj = payload.What;

            var networkMessage = new NetworkChangeVillageDemandTime(obj.VillageId, obj.LastDemandSatisfiedTime);

            network.SendAll(networkMessage);
        }

        private void HandleTradeTaxAccumulated(MessagePayload<VillageTaxAccumulateChanged> payload)
        {
            var obj = payload.What;

            var networkMessage = new NetworkChangeVillageTradeTaxAccumulated(obj.VilageId, obj.TradeTaxAccumulated);
            network.SendAll(networkMessage);
        }

        private void HandleHearth(MessagePayload<VillageHearthChanged> payload)
        {
            var obj = payload.What;

            var networkMessage = new NetworkChangeVillageHearth(obj.VillageId, obj.Hearth);
            network.SendAll(networkMessage);
        }

        private void HandleTradeBound(MessagePayload<VillageTradeBoundChanged> payload)
        {
            var obj = payload.What;

            // Broadcast to all the clients that the state was changed
            var networkMessage = new NetworkChangeVillageTradeBound(obj.VillageId, obj.TradeBoundId);
            network.SendAll(networkMessage);
        }

        private void HandleVillageState(MessagePayload<VillageStateChanged> payload)
        {
            var obj = payload.What;

            // Broadcast to all the clients that the state was changed
            var networkMessage = new NetworkChangeVillageState(obj.SettlementId, obj.State);
            network.SendAll(networkMessage);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<VillageStateChanged>(HandleVillageState);
            messageBroker.Unsubscribe<VillageTradeBoundChanged>(HandleTradeBound);

        }

    }
}
