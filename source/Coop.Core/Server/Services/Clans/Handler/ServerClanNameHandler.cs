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
    public class ServerClanNameHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanNameHandler>();

        public ServerClanNameHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkClanNameChangeRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkClanNameChangeRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkClanNameChangeRequest> obj)
        {
            var payload = obj.What;

            ClanNameChanged clanNameChanged = new ClanNameChanged(payload.ClanId, payload.Name, payload.InformalName);

            messageBroker.Publish(this, clanNameChanged);

            NetworkClanNameChangeApproved clanNameChangeApproved = new NetworkClanNameChangeApproved(payload.ClanId, payload.Name, payload.InformalName);

            network.SendAll(clanNameChangeApproved);
        }
    }
}