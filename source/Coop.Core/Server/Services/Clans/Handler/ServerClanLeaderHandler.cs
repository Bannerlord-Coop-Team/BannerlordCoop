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
    public class ServerClanLeaderHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanLeaderHandler>();

        public ServerClanLeaderHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<ClanLeaderChanged>(Handle);
            messageBroker.Subscribe<NetworkClanLeaderChangeRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanLeaderChanged>(Handle);
            messageBroker.Unsubscribe<NetworkClanLeaderChangeRequest>(Handle);
        }
        private void Handle(MessagePayload<ClanLeaderChanged> obj)
        {
            var payload = obj.What;

            ChangeClanLeader clanLeaderChanged = new ChangeClanLeader(payload.ClanId, payload.NewLeaderId);

            messageBroker.Publish(this, clanLeaderChanged);

            NetworkClanLeaderChangeApproved clanLeaderChangeApproved = new NetworkClanLeaderChangeApproved(payload.ClanId, payload.NewLeaderId);

            network.SendAll(clanLeaderChangeApproved);
        }

        private void Handle(MessagePayload<NetworkClanLeaderChangeRequest> obj)
        {
            var payload = obj.What;

            ChangeClanLeader clanLeaderChanged = new ChangeClanLeader(payload.ClanId, payload.NewLeaderId);

            messageBroker.Publish(this, clanLeaderChanged);

            NetworkClanLeaderChangeApproved clanLeaderChangeApproved = new NetworkClanLeaderChangeApproved(payload.ClanId, payload.NewLeaderId);

            network.SendAll(clanLeaderChangeApproved);
        }
    }
}