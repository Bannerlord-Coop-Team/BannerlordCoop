using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsToDeliverAHerd.Patches;

[HarmonyPatch]
internal class HeadmanHerdBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverHerdOnConsequence))]
    [HarmonyPrefix]
    private static bool DeliverPrefix(Quest __instance) => BeforeBranch(__instance, HeadmanHerdBranch.Deliver);

    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverHerdRejectOnConsequence))]
    [HarmonyPrefix]
    private static bool KeepPrefix(Quest __instance) => BeforeBranch(__instance, HeadmanHerdBranch.Keep);

    private static bool BeforeBranch(Quest quest, HeadmanHerdBranch branch)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(quest, (byte)branch);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.AddHerdAndShepherdsToMainParty))]
    [HarmonyPrefix]
    private static bool GrantHerdPrefix() => ModInformation.IsServer;

    // The server already replicated the inventory, gold and settlement effects before this replay.
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnCompleteWithSuccess))]
    [HarmonyPrefix]
    private static bool SuccessPrefix(Quest __instance)
    {
        if (ModInformation.IsServer) return true;
        __instance.AddLog(__instance.SuccessQuestLogText);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFailed))]
    [HarmonyPrefix]
    private static bool FailurePrefix(Quest __instance)
    {
        if (ModInformation.IsServer) return true;
        __instance.AddLog(__instance.FailByRejectQuestLogText);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix(Quest __instance)
    {
        if (ModInformation.IsServer) return true;
        __instance.AddLog(__instance.FailByTimeOutQuestLogText);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnCanceled))]
    [HarmonyPrefix]
    private static bool CanceledPrefix(Quest __instance)
    {
        if (ModInformation.IsServer) __instance.RemoveRemainingHorses();
        return false;
    }
}
