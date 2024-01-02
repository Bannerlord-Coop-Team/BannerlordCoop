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
            messageBroker.Subscribe<ClanInfluenceChanged>(Handle);
            messageBroker.Subscribe<NetworkChangeClanInfluenceRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanInfluenceChanged>(Handle);
            messageBroker.Unsubscribe<NetworkChangeClanInfluenceRequest>(Handle);
        }
        private void Handle(MessagePayload<ClanInfluenceChanged> obj)
        {
            var payload = obj.What;

            Send(payload.ClanId, payload.Amount);
        }

        private void Handle(MessagePayload<NetworkChangeClanInfluenceRequest> obj)
        {
            var payload = obj.What;

            Send(payload.ClanId, payload.Amount);
        }

        private void Send(string clanId, float amount)
        {
            ChangeClanInfluence changeClanInfluence = new ChangeClanInfluence(clanId, amount);

            messageBroker.Publish(this, changeClanInfluence);

            NetworkClanChangeInfluenceApproved clanChangeInfluenceApproved = new NetworkClanChangeInfluenceApproved(clanId, amount);

            network.SendAll(clanChangeInfluenceApproved);
        }
    }
}