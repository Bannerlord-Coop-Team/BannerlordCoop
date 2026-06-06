using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Inventory;

namespace GameInterface.Services.HeroDevelopers.Patches
{
    [HarmonyPatch(typeof(PerkActivationHandlerCampaignBehavior))]
    internal class PerkActivationPatch
    {
        private static readonly ILogger logger = LogManager.GetLogger<PerkActivationHandlerCampaignBehavior>();

        [HarmonyPatch(nameof(PerkActivationHandlerCampaignBehavior.OnPerkOpened))]
        [HarmonyPrefix]
        static bool OnPerkOpenedPrefix(ref InventoryLogic __instance, Hero hero, PerkObject perk)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient) return false;

            var message = new PerkOpened(hero, perk);
            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }
    }
}
