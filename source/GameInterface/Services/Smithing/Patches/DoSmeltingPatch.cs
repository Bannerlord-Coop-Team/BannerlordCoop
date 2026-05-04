using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class DoSmeltingPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch("DoSmelting")]
        [HarmonyPrefix]
        public static bool DoSmelting(ref CraftingCampaignBehavior __instance, Hero currentCraftingHero, EquipmentElement equipmentElement)
        {
            // Publish message with data
            var message = new SmeltingDone(__instance, currentCraftingHero, equipmentElement);
            MessageBroker.Instance.Publish(__instance, message);

            // AddSkillXp already synced, run on client
            currentCraftingHero.AddSkillXp(DefaultSkills.Crafting, (float)Campaign.Current.Models.SmithingModel.GetSkillXpForSmelting(equipmentElement.Item));

            // Skip original to override original client saving
            return false;
        }
    }
}