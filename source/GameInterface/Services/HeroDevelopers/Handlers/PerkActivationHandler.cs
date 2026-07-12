using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

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
            messageBroker.Subscribe<NetworkOpenPerk>(Handle_NetworkOpenPerk);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<OpenPerk>(Handle_OpenPerk);
            messageBroker.Unsubscribe<NetworkOpenPerk>(Handle_NetworkOpenPerk);
        }

        private void Handle_OpenPerk(MessagePayload<OpenPerk> obj)
        {
            var data = obj.What;

            GameThread.RunSafe(() =>
            {
                if (!TryGetPerkActivationBehavior(out var perkActivationBehavior)) return;
                if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
                if (!objectManager.TryGetIdWithLogging(obj.What.Perk, out var perkId)) return;

                OnOpenedPerkInternal(data.Hero, data.Perk);

                network.SendAll(new NetworkOpenPerk(heroId, perkId));
            });
        }

        private void Handle_NetworkOpenPerk(MessagePayload<NetworkOpenPerk> obj)
        {
            var data = obj.What;

            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
                if (!objectManager.TryGetObjectWithLogging<PerkObject>(data.PerkId, out var perk)) return;

                using (new AllowedThread())
                {
                    OnOpenedPerkInternal(hero, perk);
                }
            });
        }

        private void OnOpenedPerkInternal(Hero hero, PerkObject perk)
        {
            if (hero == null) return;

            if (perk == DefaultPerks.OneHanded.Trainer || perk == DefaultPerks.OneHanded.UnwaveringDefense || perk == DefaultPerks.TwoHanded.ThickHides || perk == DefaultPerks.Athletics.WellBuilt || perk == DefaultPerks.Medicine.PreventiveMedicine)
            {
                hero.HitPoints += (int)perk.PrimaryBonus;
            }
            else if (perk == DefaultPerks.Crafting.VigorousSmith)
            {
                hero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Vigor, 1, false);
            }
            else if (perk == DefaultPerks.Crafting.StrongSmith)
            {
                hero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Control, 1, false);
            }
            else if (perk == DefaultPerks.Crafting.EnduringSmith)
            {
                hero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Endurance, 1, false);
            }
            else if (perk == DefaultPerks.Crafting.WeaponMasterSmith)
            {
                hero.HeroDeveloper.AddFocus(DefaultSkills.OneHanded, 1, false);
                hero.HeroDeveloper.AddFocus(DefaultSkills.TwoHanded, 1, false);
            }
            else if (perk == DefaultPerks.Athletics.Durable)
            {
                hero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Endurance, 1, false);
            }
            else if (perk == DefaultPerks.Athletics.Steady)
            {
                hero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Control, 1, false);
            }
            else if (perk == DefaultPerks.Athletics.Strong)
            {
                hero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Vigor, 1, false);
            }

            // Substitute for Hero == Hero.MainHero
            if (hero.PartyBelongedTo?.IsPlayerParty() == true && (perk == DefaultPerks.OneHanded.Prestige || perk == DefaultPerks.TwoHanded.Hope || perk == DefaultPerks.Athletics.ImposingStature || perk == DefaultPerks.Bow.MerryMen || perk == DefaultPerks.Tactics.HordeLeader || perk == DefaultPerks.Scouting.MountedScouts || perk == DefaultPerks.Leadership.Authority || perk == DefaultPerks.Leadership.LeaderOfMasses || perk == DefaultPerks.Leadership.UltimateLeader))
            {
                hero.PartyBelongedTo.MemberRoster.UpdateVersion();
            }
            if (perk.PrimaryRole == PartyRole.Captain)
            {
                hero.UpdatePowerModifier();
            }
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
