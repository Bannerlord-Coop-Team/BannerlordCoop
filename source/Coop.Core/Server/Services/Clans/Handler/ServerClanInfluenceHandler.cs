using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Server.Services.Clans.Messages;
using GameInterface.Services.Clans.Messages;
using Serilog;
using System;

namespace Coop.Core.Server.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans on server.
    /// </summary>
    public class ServerClanInfluenceHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanInfluenceHandler>();

        public ServerClanInfluenceHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkChangeClanInfluenceRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkChangeClanInfluenceRequest>(Handle);
        }
       
        private void Handle(MessagePayload<NetworkChangeClanInfluenceRequest> obj)
        {
            var payload = obj.What;

            ClanInfluenceChanged clanInfluenceChanged = new ClanInfluenceChanged(payload.ClanId, payload.Amount);

            messageBroker.Publish(this, clanInfluenceChanged);

            NetworkClanChangeInfluenceApproved clanChangeInfluenceApproved = new NetworkClanChangeInfluenceApproved(payload.ClanId, payload.Amount);

            network.SendAll(clanChangeInfluenceApproved);
        }
    }
}