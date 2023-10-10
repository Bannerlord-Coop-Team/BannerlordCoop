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
    public class ServerClanRenownHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanRenownHandler>();

        public ServerClanRenownHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkAddRenownRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkAddRenownRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkAddRenownRequest> obj)
        {
            var payload = obj.What;

            ClanRenownAdded renownAdded = new ClanRenownAdded(payload.ClanId, payload.Amount, payload.ShouldNotify);

            messageBroker.Publish(this, renownAdded);

            NetworkRenownAddApproved renownAddApproved = new NetworkRenownAddApproved(payload.ClanId, payload.Amount, payload.ShouldNotify);

            network.SendAll(renownAddApproved);
        }
    }
}