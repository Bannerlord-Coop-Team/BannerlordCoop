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
    public class ClientClanLeaderHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanLeaderHandler>();

        public ClientClanLeaderHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;            

            messageBroker.Subscribe<ChangeClanLeader>(Handle);
            messageBroker.Subscribe<NetworkClanLeaderChangeApproved>(Handle);

        }

        public void Dispose()
        {

            messageBroker.Unsubscribe<ChangeClanLeader>(Handle);
            messageBroker.Unsubscribe<NetworkClanLeaderChangeApproved>(Handle);

        }

        private void Handle(MessagePayload<ChangeClanLeader> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkClanLeaderChangeRequest(payload.ClanId, payload.NewLeaderId));
        }

        private void Handle(MessagePayload<NetworkClanLeaderChangeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ClanLeaderChanged(payload.ClanId, payload.NewLeaderId));
        }
    }
}