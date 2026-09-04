using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
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

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "Success")]
internal class VillageNeedsCraftingMaterialsQuestSuccessGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix() => IssueFinalizeAuthorityGuard.IsActive;
}

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "Success")]
internal class VillageNeedsCraftingMaterialsQuestSuccessTriggerPatch
{
    [HarmonyPostfix]
    private static void Postfix(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest __instance)
    {
        if (IssueFinalizeAuthorityGuard.IsActive) return;

        var owner = __instance.QuestGiver;
        if (owner == null) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(owner, new QuestSuccessTriggered(owner, controllerIdProvider?.ControllerId));
    }
}
