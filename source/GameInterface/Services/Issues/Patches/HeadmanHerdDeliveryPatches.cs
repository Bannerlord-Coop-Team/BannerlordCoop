using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

[HarmonyPatch(typeof(Quest))]
internal static class HeadmanHerdDeliveryPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.DeliverHerdOnConsequence));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.DeliverHerdRejectOnConsequence));
    }

    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, MethodBase __originalMethod)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer) return IssueFinalizeAuthorityGuard.IsActive;
        if (!ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership)
            || !ownership.IsLocalOwner(__instance.StringId)) return false;
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controller)) return false;
        var reason = __originalMethod.Name == nameof(Quest.DeliverHerdOnConsequence)
            ? IssueFinalizeReason.QuestSuccess : IssueFinalizeReason.QuestFail;
        MessageBroker.Instance.Publish(__instance, new QuestTerminalOutcomeTriggered(__instance.QuestGiver, controller.ControllerId, reason));
        return false;
    }
}
