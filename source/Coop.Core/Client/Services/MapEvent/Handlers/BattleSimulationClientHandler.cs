using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Handler;
using Coop.Core.Client.Services.MapEvent.Messages;
using Coop.Core.Server.Services.Clans.Messages;
using Coop.Core.Server.Services.MapEvent.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MapEvents.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MapEvent.Handlers
{
    /// <summary>
    /// Client handler for Starting Map Events
    /// </summary>
    public class BattleSimulationClientHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<BattleSimulationClientHandler>();

        public BattleSimulationClientHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<BattleRoundSimulated>(Handle);
            messageBroker.Subscribe<NetworkBattleSimulatedApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BattleRoundSimulated>(Handle);
            messageBroker.Unsubscribe<NetworkBattleSimulatedApproved>(Handle);
        }
        private void Handle(MessagePayload<BattleRoundSimulated> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkBattleRoundSimulatedRequest(
                payload.PartyId, 
                payload.Side,
                payload.Advantage));
        }
        private void Handle(MessagePayload<NetworkBattleSimulatedApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new SimulateBattleRound(
                payload.PartyId,
                payload.Side,
                payload.Advantage));
        }
    }
}
