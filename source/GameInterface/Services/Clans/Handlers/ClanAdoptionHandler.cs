using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Handlers
{
    /// <summary>
    /// Handles all changes to clans on client.
    /// </summary>
    public class ClanAdoptionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ClanAdoptionHandler>();

        public ClanAdoptionHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<HeroAdopted>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<HeroAdopted>(Handle);
        }

        private void Handle(MessagePayload<HeroAdopted> obj)
        {
            var payload = obj.What;

            objectManager.TryGetObject<Clan>(payload.ClanId, out var playerClan);

            objectManager.TryGetObject<Hero>(payload.AdoptedHeroId, out var adoptedHero);

            objectManager.TryGetObject<Hero>(payload.PlayerHeroId, out var playerHero);

            ClanAdoptHeroPatch.RunFixedAdoptHero(adoptedHero, playerClan, playerHero);
        }
    }
}
