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
            messageBroker.Subscribe<ClanNameChanged>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChanged>(Handle);
            messageBroker.Unsubscribe<NetworkClanNameChangeRequest>(Handle);
        }
        private void Handle(MessagePayload<ClanNameChanged> obj)
        {
            var payload = obj.What;

            ChangeClanName clanNameChanged = new ChangeClanName(payload.ClanId, payload.Name, payload.InformalName);

            messageBroker.Publish(this, clanNameChanged);

            NetworkClanNameChangeApproved clanNameChangeApproved = new NetworkClanNameChangeApproved(payload.ClanId, payload.Name, payload.InformalName);

            network.SendAll(clanNameChangeApproved);
        }

        private void Handle(MessagePayload<NetworkClanNameChangeRequest> obj)
        {
            var payload = obj.What;

            ChangeClanName clanNameChanged = new ChangeClanName(payload.ClanId, payload.Name, payload.InformalName);

            messageBroker.Publish(this, clanNameChanged);

            NetworkClanNameChangeApproved clanNameChangeApproved = new NetworkClanNameChangeApproved(payload.ClanId, payload.Name, payload.InformalName);

            network.SendAll(clanNameChangeApproved);
        }
    }
}