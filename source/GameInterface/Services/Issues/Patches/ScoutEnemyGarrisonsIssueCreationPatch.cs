using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side creation of a
/// <see cref="ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue"/> - see
/// <see cref="IScoutEnemyGarrisonsIssueInterface"/>'s doc comment. Own independent postfix, same reasoning as
/// <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class ScoutEnemyGarrisonsIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue villageIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new ScoutEnemyGarrisonsIssueCreated(villageIssue));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="IScoutEnemyGarrisonsIssueInterface"/>'s doc comment for the full
/// derivation): gates the QUEST's whole <c>HourlyTick</c> - the only place that advances/completes scouting
/// progress, driven by the LOCAL <c>MobileParty.MainParty</c>'s position - to the recorded owner only. The
/// one cosmetic trade-off (accepted, same shape as other ambient-tick trade-offs already documented in this
/// project - see <see cref="IssueManagerTickPatches"/>): the "all targets became neutral, cancel the mission"
/// check that also lives inside vanilla's <c>HourlyTick</c> now only runs on the owner's own client instead of
/// every peer - harmless, since it's about globally-shared Settlement/Faction state and will still fire
/// correctly the next time the owner's own client ticks.
/// </summary>
[HarmonyPatch(typeof(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest), "HourlyTick")]
internal class ScoutEnemyGarrisonsOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
