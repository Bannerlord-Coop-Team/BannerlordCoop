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
    public class ClientClanAdoptionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientClanAdoptionHandler>();

        public ClientClanAdoptionHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<HeroAdopted>(Handle);
            messageBroker.Subscribe<NetworkAdoptHeroApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<HeroAdopted>(Handle);
            messageBroker.Unsubscribe<NetworkAdoptHeroApproved>(Handle);
        }

        private void Handle(MessagePayload<HeroAdopted> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkAdoptHeroRequest(payload.AdoptedHeroId, payload.ClanId, payload.PlayerHeroId));
        }

        private void Handle(MessagePayload<NetworkAdoptHeroApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new AdoptHero(payload.HeroId, payload.PlayerClanId, payload.PlayerHeroId));
        }
    }
}