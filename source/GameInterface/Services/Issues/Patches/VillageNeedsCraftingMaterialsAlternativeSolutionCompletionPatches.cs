using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior))]
internal class VillageNeedsCraftingMaterialsAlternativeSolutionCompletionPatches
{
    private static readonly ModuleRescanCompletion<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue> RescanSpec =
        new(VillageNeedsCraftingMaterialsQuestType.TryTriggerOwnedAlternativeSolutionCompletion);

    [HarmonyPatch(nameof(VillageNeedsCraftingMaterialsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix(VillageNeedsCraftingMaterialsIssueBehavior __instance)
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);
    }

    private static void OnHourlyTick()
    {
        if (ContainerProvider.TryResolve<IModuleRescanCompletionRunner>(out var runner))
        {
            runner.Run(RescanSpec);
        }
    }
}
