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
    public class EndBattleClientHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<EndBattleClientHandler>();

        public EndBattleClientHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkEndBattleApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkEndBattleApproved>(Handle);
        }

        private void Handle(MessagePayload<NetworkEndBattleApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new EndBattle(payload.partyId));
        }
    }
}
