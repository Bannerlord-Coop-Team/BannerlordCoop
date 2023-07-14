using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(FoodConsumptionBehavior))]
internal class DisableFoodConsumptionBehavior
{
    [HarmonyPatch(nameof(FoodConsumptionBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
