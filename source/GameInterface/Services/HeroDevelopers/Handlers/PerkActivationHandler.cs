using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem;
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
        private readonly IPlayerRegistry playerRegistry;

        public PerkActivationHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            IPlayerRegistry playerRegistry)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.playerRegistry = playerRegistry;
            messageBroker.Subscribe<PerkOpened>(Handle_PerkOpened);
            messageBroker.Subscribe<OpenPerk>(Handle_OpenPerk);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PerkOpened>(Handle_PerkOpened);
            messageBroker.Unsubscribe<OpenPerk>(Handle_OpenPerk);
        }

        private void Handle_PerkOpened(MessagePayload<PerkOpened> obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Perk, out var perkId)) return;

            // Run on all clients
            var message = new OpenPerk(heroId, perkId);
            network.SendAll(message);

            OnOpenedPerkInternal(obj.What.Hero, obj.What.Perk);
        }

        private void Handle_OpenPerk(MessagePayload<OpenPerk> obj)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;
            if (!objectManager.TryGetObjectWithLogging<PerkObject>(obj.What.PerkId, out var perk)) return;

            OnOpenedPerkInternal(hero, perk);
        }

        private void OnOpenedPerkInternal(Hero hero, PerkObject perk)
        {
            if (hero != null)
            {
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
                if (playerRegistry.Contains(hero.PartyBelongedTo) && (perk == DefaultPerks.OneHanded.Prestige || perk == DefaultPerks.TwoHanded.Hope || perk == DefaultPerks.Athletics.ImposingStature || perk == DefaultPerks.Bow.MerryMen || perk == DefaultPerks.Tactics.HordeLeader || perk == DefaultPerks.Scouting.MountedScouts || perk == DefaultPerks.Leadership.Authority || perk == DefaultPerks.Leadership.LeaderOfMasses || perk == DefaultPerks.Leadership.UltimateLeader))
                {
                    hero.PartyBelongedTo.MemberRoster.UpdateVersion();
                }
                if (perk.PrimaryRole == PartyRole.Captain)
                {
                    hero.UpdatePowerModifier();
                }
            }
        }
    }
}
