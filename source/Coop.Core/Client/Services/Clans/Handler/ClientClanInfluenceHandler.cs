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
    public class ClientClanInfluenceHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientClanInfluenceHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<ClanInfluenceChanged>(Handle);
            messageBroker.Subscribe<NetworkClanChangeInfluenceApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanInfluenceChanged>(Handle);
            messageBroker.Unsubscribe<NetworkClanChangeInfluenceApproved>(Handle);
        }

        private void Handle(MessagePayload<ClanInfluenceChanged> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkChangeClanInfluenceRequest(payload.ClanId, payload.Amount));
        }

        private void Handle(MessagePayload<NetworkClanChangeInfluenceApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ChangeClanInfluence(payload.ClanId, payload.Amount));
        }
    }
}