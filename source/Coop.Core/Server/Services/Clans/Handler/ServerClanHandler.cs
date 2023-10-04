using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using Coop.Core.Client.Services.Clans.Messages;
using GameInterface.Services.Clans.Messages;
using Coop.Core.Server.Services.Clans.Messages;

namespace Coop.Core.Server.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans.
    /// </summary>
    public class ServerClanHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanHandler>();

        public ServerClanHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkClanNameChangeRequest>(Handle);

            messageBroker.Subscribe<NetworkClanLeaderChangeRequest>(Handle);
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

        private void Handle(MessagePayload<NetworkClanLeaderChangeRequest> obj)
        {
            var payload = obj.What;

            ClanLeaderChanged clanLeaderChanged = new ClanLeaderChanged(payload.ClanId, payload.NewLeaderId);

            messageBroker.Publish(this, clanLeaderChanged);

            NetworkClanLeaderChangeApproved clanLeaderChangeApproved = new NetworkClanLeaderChangeApproved(payload.ClanId, payload.NewLeaderId);

            network.SendAll(clanLeaderChangeApproved);
        }
    }
}
