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
    public class EndBattleServerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<EndBattleServerHandler>();

        public EndBattleServerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<BattleEnded>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BattleEnded>(Handle);
        }
        private void Handle(MessagePayload<BattleEnded> obj)
        {
            var payload = obj.What;

            Send(payload.partyId);
        }

        private void Send(string partyId)
        {
            EndBattle endBattle = new EndBattle(partyId);

            messageBroker.Publish(this, endBattle);

            NetworkEndBattleApproved endBattleApproved = new NetworkEndBattleApproved(partyId);

            network.SendAll(endBattleApproved);
        }
    }
}