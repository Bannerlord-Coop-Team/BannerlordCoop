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
    public class StartBattleClientHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<StartBattleClientHandler>();

        public StartBattleClientHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<BattleStarted>(Handle);
            messageBroker.Subscribe<NetworkStartBattleApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BattleStarted>(Handle);
            messageBroker.Unsubscribe<NetworkStartBattleApproved>(Handle);
        }
        private void Handle(MessagePayload<BattleStarted> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkBattleStartedRequest(
                payload.attackerPartyId, 
                payload.defenderPartyId));
        }
        private void Handle(MessagePayload<NetworkStartBattleApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new StartBattle(
                payload.attackerPartyId,
                payload.defenderPartyId));
        }
    }
}
