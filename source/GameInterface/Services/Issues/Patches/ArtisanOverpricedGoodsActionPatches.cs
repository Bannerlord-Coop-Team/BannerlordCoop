using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

[HarmonyPatch(typeof(Quest))]
[HarmonyPatchCategory(GameInterface.HARMONY_ISSUE_CONSEQUENCES_CATEGORY)]
internal class ArtisanOverpricedGoodsDeliveryPatches
{
    [HarmonyPatch(nameof(Quest.DeliverItemsPartiallyOnConsequence))]
    [HarmonyPrefix]
    internal static bool Partial(Quest __instance) => Request(__instance, ArtisanOverpricedGoodsAction.DeliverPartial);

    [HarmonyPatch(nameof(Quest.DeliverItemsFullyOnConsequence))]
    [HarmonyPrefix]
    private static bool Full(Quest __instance) => Request(__instance, ArtisanOverpricedGoodsAction.DeliverFull);

    [HarmonyPatch(nameof(Quest.AcceptCounterOffer))]
    [HarmonyPrefix]
    private static bool Merchant(Quest __instance) => Request(__instance, ArtisanOverpricedGoodsAction.AcceptMerchantOffer);

    private static bool Request(Quest quest, ArtisanOverpricedGoodsAction action)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ArtisanOverpricedGoodsActionGuard.IsActiveFor(quest.QuestGiver)) return true;
        if (ContainerProvider.TryResolve<IArtisanOverpricedGoodsActions>(out var actions))
            actions.Request(quest.QuestGiver, action);
        return false;
    }

    [HarmonyPatch(nameof(Quest.IsSuitableToTalk))]
    [HarmonyPrefix]
    private static bool MerchantCondition(Quest __instance, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) &&
            ownership.IsLocalPeerOwner(__instance.QuestGiver)) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(Quest.QuestAcceptedConsequences))]
    [HarmonyPrefix]
    private static bool Accepted(Quest __instance) =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || __instance._playerStartsQuestLog == null;

    [HarmonyPatch(nameof(Quest.OnCompleteWithSuccess))]
    [HarmonyPrefix]
    private static bool Success(Quest __instance)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return false;
    }

    [HarmonyPatch(nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool Timeout(Quest __instance)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return false;
    }

    [HarmonyPatch(nameof(Quest.OnFailed))]
    [HarmonyPrefix]
    private static bool Failed() => ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))]
internal class ArtisanOverpricedGoodsChainedSuccessPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(QuestBase __instance)
    {
        if (__instance is not Quest) return true;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        // The next dialogue line cannot finish a rejected or still-pending delivery.
        return ArtisanOverpricedGoodsActionGuard.IsActiveFor(__instance.QuestGiver) ||
            (ModInformation.IsClient && IssueFinalizeAuthorityGuard.IsActive);
    }
}

[HarmonyPatch(typeof(IssueBase))]
internal class ArtisanOverpricedGoodsLordPatches
{
    [HarmonyPatch(nameof(IssueBase.StartIssueWithLordSolution))]
    [HarmonyPrefix]
    private static bool Start(IssueBase __instance) => Request(__instance, ArtisanOverpricedGoodsAction.StartLordSolution);

    [HarmonyPatch(nameof(IssueBase.CompleteIssueWithLordSolutionWithAcceptCounterOffer))]
    [HarmonyPrefix]
    private static bool Accept(IssueBase __instance) => Request(__instance, ArtisanOverpricedGoodsAction.AcceptLordOffer);

    [HarmonyPatch(nameof(IssueBase.CompleteIssueWithLordSolutionWithRefuseCounterOffer))]
    [HarmonyPrefix]
    private static bool Refuse(IssueBase __instance) => Request(__instance, ArtisanOverpricedGoodsAction.RefuseLordOffer);

    private static bool Request(IssueBase issue, ArtisanOverpricedGoodsAction action)
    {
        if (issue is not Issue) return true;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ArtisanOverpricedGoodsActionGuard.IsActiveFor(issue.IssueOwner)) return true;
        if (ModInformation.IsClient && CallOriginalPolicy.IsOriginalAllowed() &&
            action == ArtisanOverpricedGoodsAction.StartLordSolution) return true;
        if (ContainerProvider.TryResolve<IArtisanOverpricedGoodsActions>(out var actions))
            actions.Request(issue.IssueOwner, action);
        return false;
    }

    [HarmonyPatch(nameof(IssueBase.BeforeGameMenuOpened))]
    [HarmonyPrefix]
    private static bool Menu(IssueBase __instance)
    {
        if (__instance is not Issue) return true;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsClient && ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) &&
            ownership.IsLocalPeerOwner(__instance.IssueOwner);
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionStartConsequence))]
internal class ArtisanOverpricedGoodsFundingPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
        (ModInformation.IsServer && AlternativeSolutionStartAuthorityGuard.IsActive);
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnIssueUpdated))]
internal class ArtisanOverpricedGoodsMirrorRewardsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue) => issue is not Issue || ModInformation.IsServer ||
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
}
