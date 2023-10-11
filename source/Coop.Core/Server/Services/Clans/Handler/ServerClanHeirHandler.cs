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
    public class ServerClanHeirHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanHeirHandler>();

        public ServerClanHeirHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NewHeirAdded>(Handle);
            messageBroker.Subscribe<NetworkNewHeirRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NewHeirAdded>(Handle);
            messageBroker.Unsubscribe<NetworkNewHeirRequest>(Handle);
        }
        private void Handle(MessagePayload<NewHeirAdded> obj)
        {
            var payload = obj.What;

            AddNewHeir newHeir = new AddNewHeir(payload.HeirHeroId, payload.PlayerHeroId, payload.IsRetirement);

            messageBroker.Publish(this, newHeir);

            NetworkNewHeirApproved newHeirApproved = new NetworkNewHeirApproved(payload.HeirHeroId, payload.PlayerHeroId, payload.IsRetirement);

            network.SendAll(newHeirApproved);
        }

        private void Handle(MessagePayload<NetworkNewHeirRequest> obj)
        {
            var payload = obj.What;

            AddNewHeir newHeir = new AddNewHeir(payload.HeirHeroId, payload.PlayerHeroId, payload.IsRetirement);

            messageBroker.Publish(this, newHeir);

            NetworkNewHeirApproved newHeirApproved = new NetworkNewHeirApproved(payload.HeirHeroId, payload.PlayerHeroId, payload.IsRetirement);

            network.SendAll(newHeirApproved);
        }
    }
}