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
    public class ClientClanNameHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanNameHandler>();

        public ClientClanNameHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<ChangeClanName>(Handle);
            messageBroker.Subscribe<NetworkClanNameChangeApproved>(Handle);
            
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeClanName>(Handle);
            messageBroker.Unsubscribe<NetworkClanNameChangeApproved>(Handle);
        }

        private void Handle(MessagePayload<ChangeClanName> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanNameChangeRequest(payload.ClanId, payload.Name, payload.InformalName));
        }

        private void Handle(MessagePayload<NetworkClanNameChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanNameChanged(payload.ClanId, payload.Name, payload.InformalName));

        }
    }
}