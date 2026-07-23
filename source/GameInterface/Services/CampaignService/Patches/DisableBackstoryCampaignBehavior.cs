using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(BackstoryCampaignBehavior))]
internal class DisableBackstoryCampaignBehavior
{
    [HarmonyPatch(nameof(BackstoryCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
