using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest), "FinishQuestSuccess1")]
internal class VillageNeedsToolsQuestSuccessGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix() => IssueFinalizeAuthorityGuard.IsActive;
}

[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest), "FinishQuestSuccess1")]
internal class VillageNeedsToolsQuestSuccessTriggerPatch
{
    [HarmonyPostfix]
    private static void Postfix(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest __instance)
    {
        if (IssueFinalizeAuthorityGuard.IsActive) return;

        var owner = __instance.QuestGiver;
        if (owner == null) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(owner, new QuestSuccessTriggered(owner, controllerIdProvider?.ControllerId));
    }
}

[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest), "RaidCompleted")]
internal class VillageNeedsToolsQuestRaidCompletedAuthorityPatch
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

[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest), "PlayerHasTools")]
internal class VillageNeedsToolsQuestOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest __instance, ref bool __result)
    {
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) ||
            !ownershipRegistry.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            return false;
        }

        return true;
    }
}
