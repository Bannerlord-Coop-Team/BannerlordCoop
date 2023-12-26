using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Server.Services.Clans.Messages;
using GameInterface.Services.Clans.Messages;
using Serilog;

namespace Coop.Core.Client.Services.Clans.Handler
{
    /// <summary>
    /// Handles all changes to clans.
    /// </summary>
    public class ClientClanKingdomHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanKingdomHandler>();

        public ClientClanKingdomHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<ClanKingdomChanged>(Handle);
            messageBroker.Subscribe<NetworkClanKingdomChangeApproved>(Handle);
        }

        public void Dispose()
        {

            messageBroker.Unsubscribe<ClanKingdomChanged>(Handle);
            messageBroker.Unsubscribe<NetworkClanKingdomChangeApproved>(Handle);

        }

        private void Handle(MessagePayload<ClanKingdomChanged> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanKingdomChangeRequest(payload.ClanId, payload.NewKingdomId, 
                (int)payload.Detail, payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification));
        }

        private void Handle(MessagePayload<NetworkClanKingdomChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ChangeClanKingdom(payload.ClanId, payload.NewKingdomId, payload.DetailId, 
                payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification));
        }
    }
}