using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "CompleteQuestClickableConditions")]
internal class VillageNeedsCraftingMaterialsQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest __instance, ref bool __result, out TextObject explanation)
    {
        var isOwner = ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) &&
            ownershipRegistry.IsLocalPeerOwner(__instance.QuestGiver);
        if (!isOwner)
        {
            __result = false;
            explanation = new TextObject("{=!}You don't have enough crafting materials.");
            return false;
        }

        explanation = null;
        return true;
    }
}
