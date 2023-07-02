using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(StatisticsCampaignBehavior))]
internal class DisableStatisticsCampaignBehavior
{
    [HarmonyPatch(nameof(StatisticsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
