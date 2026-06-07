using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(NearbyBanditBaseIssueBehavior))]
internal class DisableNearbyBanditBaseIssueBehavior
{
    [HarmonyPatch(nameof(NearbyBanditBaseIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
