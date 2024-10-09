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
            messageBroker.Subscribe<AdoptHero>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<AdoptHero>(Handle);
        }

        private void Handle(MessagePayload<AdoptHero> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<Clan>(payload.ClanId, out var playerClan) == false)
            {
                Logger.Error("Unable to find clan ({clanId})", payload.ClanId);
                return;
            }

            if (objectManager.TryGetObject<Hero>(payload.AdoptedHeroId, out var adoptedHero) == false)
            {
                Logger.Error("Unable to find adopted hero ({heroId})", payload.AdoptedHeroId);
                return;
            }

            if (objectManager.TryGetObject<Hero>(payload.PlayerHeroId, out var playerHero) == false)
            {
                Logger.Error("Unable to find player hero ({heroId})", payload.PlayerHeroId);
                return;
            }

            //ClanAdoptHeroPatch.RunFixedAdoptHero(adoptedHero, playerClan, playerHero);
        }
    }
}
