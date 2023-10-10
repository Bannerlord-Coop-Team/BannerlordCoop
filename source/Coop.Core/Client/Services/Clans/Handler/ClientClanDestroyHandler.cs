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
    public class ClientClanDestroyHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanDestroyHandler>();

        public ClientClanDestroyHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<DestroyClan>(Handle);
            messageBroker.Subscribe<NetworkDestroyClanApproved>(Handle);

        }

        public void Dispose()
        {


            messageBroker.Unsubscribe<DestroyClan>(Handle);
            messageBroker.Unsubscribe<NetworkDestroyClanApproved>(Handle);

        }

        private void Handle(MessagePayload<DestroyClan> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkDestroyClanRequest(payload.ClanId, payload.Details));
        }

        private void Handle(MessagePayload<NetworkDestroyClanApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanDestroyed(payload.ClanId, payload.DetailId));
        }
    }
}