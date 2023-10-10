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
    public class ServerClanKingdomHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanKingdomHandler>();

        public ServerClanKingdomHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkClanKingdomChangeRequest>(Handle);
        }

        public void Dispose()
        {
            
            messageBroker.Unsubscribe<NetworkClanKingdomChangeRequest>(Handle);

        }

        private void Handle(MessagePayload<NetworkClanKingdomChangeRequest> obj)
        {
            var payload = obj.What;

            ChangeClanKingdom clanKingdomChanged = new ChangeClanKingdom(payload.ClanId, payload.NewKingdomId, payload.DetailId, 
                payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification);

            messageBroker.Publish(this, clanKingdomChanged);

            NetworkClanKingdomChangeApproved clanKingdomChangeApproved = new NetworkClanKingdomChangeApproved(payload.ClanId, payload.NewKingdomId,
                payload.DetailId, payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification);

            network.SendAll(clanKingdomChangeApproved);
        }
    }
}