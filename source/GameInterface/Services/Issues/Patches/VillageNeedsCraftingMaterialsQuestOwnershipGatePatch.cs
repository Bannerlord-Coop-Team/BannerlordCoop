using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest))]
internal class VillageNeedsCraftingMaterialsQuestFailBranchObserverPatches
{
    [HarmonyPatch("OnTimedOut")]
    [HarmonyPrefix]
    private static void OnTimedOutPrefix(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest __instance) =>
        VillageNeedsCraftingMaterialsQuestType.ObserveQuestFail(__instance, VillageNeedsCraftingMaterialsQuestType.ProofFailTimeout);
}

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

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "QuestAcceptedConsequences")]
internal class VillageNeedsCraftingMaterialsQuestAcceptedConsequencesOncePatch
{
    [HarmonyPrefix]
    private static bool Prefix(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest __instance) =>
        __instance._playerAcceptedQuestLog == null;
}

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "UpdateQuestLog")]
internal class VillageNeedsCraftingMaterialsQuestProgressCallbackPatch
{
    [HarmonyPrefix]
    private static bool Prefix(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest __instance)
    {
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry))
        {
            return CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
        }

        var isLocalPeerOwner = ownershipRegistry.IsLocalPeerOwner(__instance.QuestGiver);
        var ownerParty = isLocalPeerOwner ? MobileParty.MainParty : null;
        if (ownerParty == null && !VillageNeedsCraftingMaterialsQuestType.TryResolveRecordedOwner(__instance.QuestGiver, out _, out ownerParty))
        {
            return CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
        }

        if (ownerParty == null || __instance._playerAcceptedQuestLog == null) return false;

        var itemNumber = ownerParty.ItemRoster.GetItemNumber(__instance._requestedItem);
        if (isLocalPeerOwner && itemNumber >= __instance._requestedItemAmount)
        {
            var textObject = new TextObject("{=MTCrXEvj}You have enough {ITEM} to complete the quest. Return to {QUEST_SETTLEMENT} to hand it over.");
            textObject.SetTextVariable("QUEST_SETTLEMENT", __instance.QuestGiver.CurrentSettlement.Name);
            textObject.SetTextVariable("ITEM", __instance._requestedItem.Name);
            MBInformationManager.AddQuickInformation(textObject);
        }

        __instance._playerAcceptedQuestLog.UpdateCurrentProgress(Math.Min(itemNumber, __instance._requestedItemAmount));
        __instance.CheckIfPlayerReadyToReturnItems();
        return false;
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
        MessageBroker.Instance.Publish(owner, new QuestTerminalOutcomeTriggered(owner, controllerIdProvider?.ControllerId, IssueFinalizeReason.QuestSuccess));
    }
}

[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest), "OnRaidCompleted")]
internal class VillageNeedsCraftingMaterialsQuestRaidCompletedAuthorityPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(out IssueFinalizeAuthorityGuard __state)
    {
        if (ModInformation.IsServer)
        {
            __state = new IssueFinalizeAuthorityGuard();
            return true;
        }

        __state = null;
        return CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
    }

    [HarmonyFinalizer]
    private static void Finalizer(IssueFinalizeAuthorityGuard __state) => __state?.Dispose();
}
