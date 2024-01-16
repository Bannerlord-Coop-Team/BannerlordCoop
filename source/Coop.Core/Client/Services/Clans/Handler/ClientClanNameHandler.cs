using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Client.States;
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
        private readonly IClientLogic clientLogic;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanNameHandler>();

        public ClientClanNameHandler(IMessageBroker messageBroker, INetwork network, IClientLogic clientLogic)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.clientLogic = clientLogic;
            messageBroker.Subscribe<ClanNameChanged>(Handle);
            messageBroker.Subscribe<NetworkClanNameChangeApproved>(Handle);
            
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChanged>(Handle);
            messageBroker.Unsubscribe<NetworkClanNameChangeApproved>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChanged> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanNameChangeRequest(payload.ClanId, payload.Name, payload.InformalName));
        }

        private void Handle(MessagePayload<NetworkClanNameChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ChangeClanName(payload.ClanId, payload.Name, payload.InformalName));

        }
    }
}