using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class DoRefinementPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch("DoRefinement")]
        [HarmonyPrefix]
        public static bool DoRefinement(ref CraftingCampaignBehavior __instance, Hero hero, Crafting.RefiningFormula refineFormula)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Publish message with data
            var message = new RefinementDone(__instance, hero, refineFormula);
            MessageBroker.Instance.Publish(__instance, message);

            // AddSkillXp already synced, run on client
            hero.AddSkillXp(DefaultSkills.Crafting, (float)Campaign.Current.Models.SmithingModel.GetSkillXpForRefining(ref refineFormula));

            // Skip original to override original client saving
            return false;
        }
    }
}