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
    public class ServerClanDestroyHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanDestroyHandler>();

        public ServerClanDestroyHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkDestroyClanRequest>(Handle);

        }

        public void Dispose()
        {

            messageBroker.Unsubscribe<NetworkDestroyClanRequest>(Handle);

        }

        private void Handle(MessagePayload<NetworkDestroyClanRequest> obj)
        {
            var payload = obj.What;

            ClanDestroyed clanDestroyed = new ClanDestroyed(payload.ClanId, payload.DetailId);

            messageBroker.Publish(this, clanDestroyed);

            NetworkDestroyClanApproved destroyClanApproved = new NetworkDestroyClanApproved(payload.ClanId, payload.DetailId);

            network.SendAll(destroyClanApproved);
        }
    }
}