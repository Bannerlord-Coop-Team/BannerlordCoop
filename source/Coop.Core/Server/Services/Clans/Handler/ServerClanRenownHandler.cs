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

            messageBroker.Subscribe<ClanRenownAdded>(Handle);
            messageBroker.Subscribe<NetworkAddRenownRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanRenownAdded>(Handle);
            messageBroker.Unsubscribe<NetworkAddRenownRequest>(Handle);
        }
        private void Handle(MessagePayload<ClanRenownAdded> obj)
        {
            var payload = obj.What;

            Send(payload.ClanId, payload.Amount, payload.ShouldNotify);
        }

        private void Handle(MessagePayload<NetworkAddRenownRequest> obj)
        {
            var payload = obj.What;

            Send(payload.ClanId, payload.Amount, payload.ShouldNotify);
        }

        private void Send(string clanId, float amount, bool shouldNotify)
        {
            AddClanRenown renownAdded = new AddClanRenown(clanId, amount, shouldNotify);

            messageBroker.Publish(this, renownAdded);

            NetworkRenownAddApproved renownAddApproved = new NetworkRenownAddApproved(clanId, amount, shouldNotify);

            network.SendAll(renownAddApproved);
        }
    }
}