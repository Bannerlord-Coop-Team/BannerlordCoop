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

            messageBroker.Subscribe<ClanKingdomChanged>(Handle);
            messageBroker.Subscribe<NetworkClanKingdomChangeRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanKingdomChanged>(Handle);
            messageBroker.Unsubscribe<NetworkClanKingdomChangeRequest>(Handle);

        }
        private void Handle(MessagePayload<ClanKingdomChanged> obj)
        {
            var payload = obj.What;

            Send(payload.ClanId, payload.NewKingdomId, payload.Detail, payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification);

        }

        private void Handle(MessagePayload<NetworkClanKingdomChangeRequest> obj)
        {
            var payload = obj.What;

            Send(payload.ClanId, payload.NewKingdomId, payload.DetailId, payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification);
        }

        private void Send(string clanId, string newKingdomId, int detailId, int awardMultiplier, bool byRebellion, bool showNotification)
        {
            ChangeClanKingdom clanKingdomChanged = new ChangeClanKingdom(clanId, newKingdomId, detailId,
                awardMultiplier, byRebellion, showNotification);

            messageBroker.Publish(this, clanKingdomChanged);

            NetworkClanKingdomChangeApproved clanKingdomChangeApproved = new NetworkClanKingdomChangeApproved(clanId, newKingdomId, detailId,
                awardMultiplier, byRebellion, showNotification);

            network.SendAll(clanKingdomChangeApproved);
        }
    }
}