using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Framework.Rewards;

/// <summary>
/// Vanilla rewards an issue's solver when the issue ends (Oratory renown and influence, giver relation, issue cooldown).
/// The server already applies them as the owning player, so a client replaying the ending for its journal leaves them alone.
/// </summary>
[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnIssueUpdated))]
internal class IssueSolverRewardsPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}
