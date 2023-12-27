using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Client.Services.MapEvent.Messages;
using Coop.Core.Server.Services.Clans.Handler;
using Coop.Core.Server.Services.Clans.Messages;
using Coop.Core.Server.Services.MapEvent.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MapEvents.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Coop.Core.Server.Services.MapEvent.Handlers
{
    public class StartBattleServerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<StartBattleServerHandler>();

        public StartBattleServerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<BattleStarted>(Handle);
            messageBroker.Subscribe<NetworkBattleStartedRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BattleStarted>(Handle);
            messageBroker.Unsubscribe<NetworkBattleStartedRequest>(Handle);
        }
        private void Handle(MessagePayload<BattleStarted> obj)
        {
            var payload = obj.What;

            Send(payload.attackerPartyId, payload.defenderPartyId);
        }
        private void Handle(MessagePayload<NetworkBattleStartedRequest> obj)
        {
            var payload = obj.What;

            Send(payload.attackerPartyId, payload.defenderPartyId);
        }

        private void Send(string attackerPartyId, string defenderPartyId)
        {
            StartBattle startBattle = new StartBattle(attackerPartyId, defenderPartyId);

            messageBroker.Publish(this, startBattle);

            NetworkStartBattleApproved startBattleApproved = new NetworkStartBattleApproved(
                attackerPartyId,
                defenderPartyId);

            network.SendAll(startBattleApproved);
        }
    }
}