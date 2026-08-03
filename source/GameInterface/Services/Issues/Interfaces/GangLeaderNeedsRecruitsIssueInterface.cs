using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.GangLeaderNeedsRecruitsAcceptancePatches"/>/
/// <see cref="Handlers.GangLeaderNeedsRecruitsIssueHandler"/> need to capture and authoritatively force-write
/// a genuine accept's terms onto every other peer's mirrored
/// <see cref="GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssueQuest"/>. The survey's
/// "deterministic tier-lookup reward" characterization is right about the REWARD (it's built up incrementally
/// from delivered recruits' tiers via <c>RewardForEachRecruit</c>, a local-only party-screen action that only
/// ever runs on the genuine owner's own machine - no cross-peer divergence risk there, same as any other
/// dialogue/party-screen-gated local action), but misses that <c>_requestedRecruitCount</c> - the quota that
/// gates completion - is computed by <c>GenerateIssueQuest</c> (called from <c>IssueBase.StartIssueWithQuest</c>,
/// i.e. at ACCEPT time) from <c>base.IssueDifficultyMultiplier</c>, which <c>StartIssueWithQuest</c> re-rolls
/// fresh immediately before calling it - the exact same per-client-at-accept-time mechanism Landlord Training
/// for Retainers and Village Needs Crafting Materials both needed their own force-write for.
/// </summary>
public interface IGangLeaderNeedsRecruitsIssueInterface : IGameAbstraction
{
    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(Hero owner, out int requestedRecruitCount);

    void MirrorQuestAccepted(Hero owner, int requestedRecruitCount);
}

/// <inheritdoc cref="IGangLeaderNeedsRecruitsIssueInterface"/>
public class GangLeaderNeedsRecruitsIssueInterface : IGangLeaderNeedsRecruitsIssueInterface
{
    private static readonly FieldInfo RequestedRecruitCountField =
        AccessTools.Field(typeof(GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssueQuest), "_requestedRecruitCount");

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out int requestedRecruitCount)
    {
        requestedRecruitCount = 0;

        if (owner?.Issue?.IssueQuest is not GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssueQuest quest) return false;

        requestedRecruitCount = (int)RequestedRecruitCountField.GetValue(quest);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, int requestedRecruitCount)
    {
        if (owner?.Issue is not GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssueQuest quest) return;

            RequestedRecruitCountField.SetValue(quest, requestedRecruitCount);
        }
    }
}
