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

            messageBroker.Subscribe<HeroAdopted>(Handle);
            messageBroker.Subscribe<NetworkAdoptHeroRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<HeroAdopted>(Handle);
            messageBroker.Unsubscribe<NetworkAdoptHeroRequest>(Handle);
        }

        private void Handle(MessagePayload<HeroAdopted> obj)
        {
            var payload = obj.What;

            Send(payload.AdoptedHeroId, payload.ClanId, payload.PlayerHeroId);
        }

        private void Handle(MessagePayload<NetworkAdoptHeroRequest> obj)
        {
            var payload = obj.What;

            Send(payload.HeroId, payload.PlayerClanId, payload.PlayerHeroId);
        }

        private void Send(string adoptedHeroId, string clanId, string playerHeroId)
        {
            AdoptHero adoptHero = new AdoptHero(adoptedHeroId, clanId, playerHeroId);

            messageBroker.Publish(this, adoptHero);

            NetworkAdoptHeroApproved adoptHeroApproved = new NetworkAdoptHeroApproved(adoptedHeroId, clanId, playerHeroId);

            network.SendAll(adoptHeroApproved);
        }
    }
}