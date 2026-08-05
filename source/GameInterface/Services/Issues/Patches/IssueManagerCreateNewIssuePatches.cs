using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Blocks a client from directly originating a new issue (creation is server-authoritative; see
/// <see cref="IssuesCampaignBehaviorGenerationPatches"/> for why), and captures + broadcasts the result of
/// a genuine server-side creation so every client replicates the exact same rolled terms instead of
/// independently re-deriving them - see <see cref="Interfaces.VillageNeedsToolsIssueInterface.ConstructReplicated"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class IssueManagerCreateNewIssuePatches
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPrefix]
    private static bool Prefix()
    {
        // The replay path (Interfaces.VillageNeedsToolsIssueInterface.RegisterReplicated) runs under
        // AllowedThread on a client so its own hand-built PotentialIssueData reaches the real method.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        // Only broadcast a genuine server-side creation. A replayed creation on a client also reaches this
        // postfix (Harmony runs postfixes regardless of which branch let the prefix through), so gate on
        // ModInformation instead of CallOriginalPolicy - replays only ever happen on clients.
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue villageIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new VillageIssueCreated(villageIssue));
    }
}
