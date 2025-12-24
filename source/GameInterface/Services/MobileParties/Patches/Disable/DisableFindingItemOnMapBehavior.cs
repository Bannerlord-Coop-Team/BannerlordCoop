using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(FindingItemOnMapBehavior))]
internal class DisableFindingItemOnMapBehavior
{
    [HarmonyPatch(nameof(FindingItemOnMapBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
