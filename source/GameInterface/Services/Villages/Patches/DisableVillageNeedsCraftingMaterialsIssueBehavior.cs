using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior))]
internal class DisableVillageNeedsCraftingMaterialsIssueBehavior
{
    [HarmonyPatch(nameof(VillageNeedsCraftingMaterialsIssueBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
