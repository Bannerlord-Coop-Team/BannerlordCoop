using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Handlers
{
    internal class PerkActivationHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PerkActivationHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public PerkActivationHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<OpenPerk>(Handle_OpenPerk);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<OpenPerk>(Handle_OpenPerk);
        }

        private void Handle_OpenPerk(MessagePayload<OpenPerk> obj)
        {
            var data = obj.What;

            GameThread.RunSafe(() =>
            {
                if (!TryGetPerkActivationBehavior(out var perkActivationBehavior)) return;

                var hero = data.Hero;
                var perk = data.Perk;

                // Replace Hero == Hero.MainHero
                if (hero.PartyBelongedTo?.IsPlayerParty() == true && (perk == DefaultPerks.OneHanded.Prestige || perk == DefaultPerks.TwoHanded.Hope || perk == DefaultPerks.Athletics.ImposingStature || perk == DefaultPerks.Bow.MerryMen || perk == DefaultPerks.Tactics.HordeLeader || perk == DefaultPerks.Scouting.MountedScouts || perk == DefaultPerks.Leadership.Authority || perk == DefaultPerks.Leadership.LeaderOfMasses || perk == DefaultPerks.Leadership.UltimateLeader))
                {
                    hero.PartyBelongedTo.MemberRoster.UpdateVersion();
                }

                // Hero == Hero.MainHero won't be true on the server. Safe to run the behavior's logic without a custom implementation
                perkActivationBehavior.OnPerkOpened(hero, perk);
            });
        }

        private bool TryGetPerkActivationBehavior(out PerkActivationHandlerCampaignBehavior perkActivationBehavior)
        {
            perkActivationBehavior = Campaign.Current?.GetCampaignBehavior<PerkActivationHandlerCampaignBehavior>();
            if (perkActivationBehavior != null) return true;

            Logger.Debug("Skipping perk activation because the campaign behavior is unavailable");
            return false;
        }
    }
}
