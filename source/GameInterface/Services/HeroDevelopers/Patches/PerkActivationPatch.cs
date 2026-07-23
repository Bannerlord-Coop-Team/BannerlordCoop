using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Patches
{
    [HarmonyPatch(typeof(PerkActivationHandlerCampaignBehavior))]
    internal class PerkActivationPatch
    {
        private static readonly ILogger logger = LogManager.GetLogger<PerkActivationHandlerCampaignBehavior>();

        [HarmonyPatch(nameof(PerkActivationHandlerCampaignBehavior.OnPerkOpened))]
        [HarmonyPrefix]
        static bool OnPerkOpenedPrefix(ref PerkActivationHandlerCampaignBehavior __instance, Hero hero, PerkObject perk)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;

            if (hero.PartyBelongedTo?.IsPlayerParty() == true && (perk == DefaultPerks.OneHanded.Prestige || perk == DefaultPerks.TwoHanded.Hope || perk == DefaultPerks.Athletics.ImposingStature || perk == DefaultPerks.Bow.MerryMen || perk == DefaultPerks.Tactics.HordeLeader || perk == DefaultPerks.Scouting.MountedScouts || perk == DefaultPerks.Leadership.Authority || perk == DefaultPerks.Leadership.LeaderOfMasses || perk == DefaultPerks.Leadership.UltimateLeader))
            {
                hero.PartyBelongedTo.MemberRoster.UpdateVersion();

                // Publish message to update roster version on clients
                var message = new UpdateRosterVersionAfterPerkActivation(hero.PartyBelongedTo.MemberRoster);
                MessageBroker.Instance.Publish(__instance, message);
            }

            // Hero == Hero.MainHero won't be true on the server. Safe to run the behavior's logic without a custom implementation
            return true;
        }
    }
}
