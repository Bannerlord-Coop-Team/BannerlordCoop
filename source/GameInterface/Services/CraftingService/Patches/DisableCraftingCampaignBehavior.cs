using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.CraftingService.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class DisableCraftingCampaignBehavior
    {
        [HarmonyPatch(nameof(CraftingCampaignBehavior.RegisterEvents))]
        static bool Prefix() => false;
    }
}