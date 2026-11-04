using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(Quest), nameof(Quest.Success))]
internal class VillageNeedsCraftingMaterialsSuccessPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        // The server hands the materials over as the owning player, this client waits for the result
        return dispatcher.BeforeQuestBranch(__instance, (byte)VillageNeedsCraftingMaterialsBranch.SuccessByHandingOverMaterials);
    }
}
