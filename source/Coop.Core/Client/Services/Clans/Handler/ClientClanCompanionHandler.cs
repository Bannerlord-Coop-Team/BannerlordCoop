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
    public class ClientClanCompanionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanCompanionHandler>();

        public ClientClanCompanionHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<CompanionAdded>(Handle);
            messageBroker.Subscribe<NetworkCompanionAddApproved>(Handle);
        }

        public void Dispose()
        {

            messageBroker.Unsubscribe<CompanionAdded>(Handle);
            messageBroker.Unsubscribe<NetworkCompanionAddApproved>(Handle);

        }

        private void Handle(MessagePayload<CompanionAdded> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkAddCompanionRequest(payload.ClanId, payload.CompanionId));
        }

        private void Handle(MessagePayload<NetworkCompanionAddApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddCompanion(payload.ClanId, payload.CompanionId));
        }
    }
}