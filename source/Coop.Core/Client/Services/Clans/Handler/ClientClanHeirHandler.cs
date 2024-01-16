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
    public class ClientClanHeirHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanHeirHandler>();

        public ClientClanHeirHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            
            messageBroker.Subscribe<NewHeirAdded>(Handle);
            messageBroker.Subscribe<NetworkNewHeirApproved>(Handle);
        }

        public void Dispose()
        {

            messageBroker.Unsubscribe<NewHeirAdded>(Handle);
            messageBroker.Unsubscribe<NetworkNewHeirApproved>(Handle);
        }

        private void Handle(MessagePayload<NewHeirAdded> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkNewHeirRequest(payload.HeirHeroId, payload.PlayerHeroId, payload.IsRetirement));
        }
        private void Handle(MessagePayload<NetworkNewHeirApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AddNewHeir(payload.HeirHeroId, payload.PlayerHeroId, payload.IsRetirement));
        }
    }
}