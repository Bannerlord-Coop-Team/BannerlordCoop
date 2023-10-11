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
    public class ClientClanRenownHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanRenownHandler>();

        public ClientClanRenownHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<ClanRenownAdded>(Handle);
            messageBroker.Subscribe<NetworkRenownAddApproved>(Handle);

        }

        public void Dispose()
        {

            messageBroker.Unsubscribe<ClanRenownAdded>(Handle);
            messageBroker.Unsubscribe<NetworkRenownAddApproved>(Handle);

        }

        private void Handle(MessagePayload<ClanRenownAdded> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkAddRenownRequest(payload.ClanId, payload.Amount, payload.ShouldNotify));
        }
        private void Handle(MessagePayload<NetworkRenownAddApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddClanRenown(payload.ClanId, payload.Amount, payload.ShouldNotify));
        }
    }
}