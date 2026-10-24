using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssueQuest;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class MerchantOutlawsOutcomePatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.SuccessConsequences))]
    [HarmonyPrefix]
    private static bool SuccessPrefix(Quest __instance)
    {
        return !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) ||
            dispatcher.BeforeQuestBranch(__instance, MerchantOutlawsFinalizationProofStrategy.Success);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.AddQuestStepLog))]
    [HarmonyPrefix]
    private static bool ProgressPrefix(Quest __instance)
    {
        if (ModInformation.IsClient) return false;

        // The server mirror has no acceptance dialogue and therefore no progress log.
        __instance._questProgressLogTest?.UpdateCurrentProgress(__instance._questPartyProgress);
        if (ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) progress.Send(__instance);
        if (__instance._questPartyProgress >= __instance._totalPartyCount) __instance.SuccessConsequences();
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool FinalizePrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            if (ContainerProvider.TryResolve<IMerchantOutlawsProgress>(out var progress)) progress.Send(__instance, final: true);
            return true;
        }

        // AI cleanup belongs to the server; the owner only removes its map markers.
        foreach (var party in __instance._validPartiesList)
        {
            if (__instance.IsTracked(party)) __instance.RemoveTrackedObject(party);
        }

        __instance._validPartiesList.Clear();
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPrefix]
    private static bool AcceptedPrefix(Quest __instance)
    {
        // A refusal or world cancellation can arrive before the player closes the offer dialogue.
        return __instance.IsOngoing && __instance._questProgressLogTest == null &&
            IssueManager.GetIssueOfQuest(__instance) != null;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPostfix]
    private static void AcceptedPostfix(Quest __instance)
    {
        __instance._questProgressLogTest?.UpdateCurrentProgress(__instance._questPartyProgress);
    }
}
