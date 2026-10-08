using GameInterface.Policies;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(SmugglersIssueBehavior.SmugglersIssue), "IssueQuestCanBeDuplicated", MethodType.Getter)]
internal class SmugglersDuplicateQuestPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!ContainerProvider.TryResolve<ISmugglersQuestOwners>(out var owners)) return true;
        // Keep vanilla's refusal text while excluding other players' commitments.
        __result = !owners.HasActiveCommitment(Hero.MainHero);
        return false;
    }
}
