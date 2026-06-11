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
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class SetCraftedWeaponNamePatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch("SetCraftedWeaponName")]
        [HarmonyPrefix]
        public static bool SetCraftedWeaponName(ref CraftingCampaignBehavior __instance, ItemObject craftedWeaponItem, TextObject name)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Publish message with data
            var message = new BehaviorCraftedWeaponNameSet(__instance, craftedWeaponItem.StringId, name);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original to override original client saving
            return false;
        }
    }
}