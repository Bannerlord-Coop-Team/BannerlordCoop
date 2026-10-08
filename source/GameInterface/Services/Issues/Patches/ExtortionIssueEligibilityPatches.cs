using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueQuestCanBeDuplicated), MethodType.Getter)]
internal class ExtortionIssueDuplicateEligibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, ref bool __result)
    {
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!ContainerProvider.TryResolve<IExtortionQuestContext>(out var context)) return true;

        // The vanilla duplicate check must only consider this player's commitments.
        __result = !context.HasActiveIssue(Hero.MainHero);
        return false;
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionCondition))]
internal class ExtortionAlternativeEligibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(Issue __instance, ref TextObject explanation, ref bool __result)
    {
        if (!__result || ModInformation.IsClient || !AlternativeSolutionStartAuthorityGuard.IsActive) return;
        __result = __instance.CheckPreconditions(__instance.IssueOwner, out explanation);
    }
}
