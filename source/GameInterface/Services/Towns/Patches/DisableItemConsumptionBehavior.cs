using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Towns.Patches;

[HarmonyPatch(typeof(ItemConsumptionBehavior))]
internal class DisableItemConsumptionBehavior
{
    [HarmonyPatch(nameof(ItemConsumptionBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
