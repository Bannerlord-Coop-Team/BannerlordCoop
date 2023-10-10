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
    public class ServerClanAdoptionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerClanAdoptionHandler>();

        public ServerClanAdoptionHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkAdoptHeroRequest>(Handle);

        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkAdoptHeroRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkAdoptHeroRequest> obj)
        {
            var payload = obj.What;

            HeroAdopted heroAdopted = new HeroAdopted(payload.HeroId, payload.PlayerClanId, payload.PlayerHeroId);

            messageBroker.Publish(this, heroAdopted);

            NetworkAdoptHeroApproved adoptHeroApproved = new NetworkAdoptHeroApproved(payload.HeroId, payload.PlayerClanId, payload.PlayerHeroId);

            network.SendAll(adoptHeroApproved);
        }
    }
}