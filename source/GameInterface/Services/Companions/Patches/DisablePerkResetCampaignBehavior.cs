using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Companions.Patches
{
    [HarmonyPatch(typeof(PerkResetCampaignBehavior))]
    internal class DisablePerkResetCampaignBehavior
    {
        [HarmonyPatch(nameof(PerkResetCampaignBehavior.RegisterEvents))]
        static bool Prefix() => false;
    }
}