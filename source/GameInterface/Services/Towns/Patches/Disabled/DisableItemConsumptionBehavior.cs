using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(ItemConsumptionBehavior))]
internal class DisableItemConsumptionBehavior
{
    [HarmonyPatch(nameof(ItemConsumptionBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
