using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.LadysKnightOutAcceptancePatches"/>/
/// <see cref="Handlers.LadysKnightOutIssueHandler"/> need to capture and authoritatively force-write a genuine
/// accept's <c>_difficultyMultiplier</c> onto every other peer's mirrored
/// <see cref="LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest"/>. The survey's characterization ("already
/// frozen into a readonly field at accept") is correct as far as it goes for the QUEST-SOLUTION path - the
/// field genuinely is <c>readonly</c> and only ever set once, from a constructor parameter - but the value
/// PASSED IN is <c>base.IssueDifficultyMultiplier</c>, read inside <c>GenerateIssueQuest</c> (called from
/// <c>IssueBase.StartIssueWithQuest</c>, i.e. at ACCEPT time, after <c>StartIssueWithQuest</c> has already
/// re-rolled it fresh) - genuinely per-client, same mechanism as every other force-write type here. This value
/// gates real gameplay outcome (<c>TournamentRoundGoal</c>, compared against the round the player is
/// eliminated in), so it needs the same capture-and-force-write Village Needs Crafting Materials pioneered,
/// not just a cosmetic-only correction.
///
/// The survey's characterization also didn't cover this type's SECOND acceptance path,
/// <c>IsThereLordSolution == true</c> (use influence to have another lord hold the tournament instead) - the
/// FIRST issue type surveyed so far with one. No existing sync mechanism in this project handles
/// <c>IssueBase.StartIssueWithLordSolution</c> at all (unlike quest-solution/alternative-solution, which both
/// have established capture/mirror patterns) - building one from scratch (its own dialogue flow inside
/// <c>LordConversationsCampaignBehavior</c>, a <c>CounterOfferHero</c>/<c>BeforeGameMenuOpenedEvent</c> state
/// machine that is currently local-only by design, its own accept-race arbitration, etc.) is out of scope for
/// this pass. Rather than ship it silently unsynced (every peer's own issue object would diverge to a
/// different, un-broadcast <c>IssueState.SolvingWithLordSolution</c>), <see cref="Patches.LadysKnightOutAcceptancePatches"/>
/// disables the option entirely for this issue type (forces its <c>IsThereLordSolution</c> getter to false),
/// the same "disable what isn't safe yet" philosophy <c>DisableAllIssueBehaviorsExceptAllowlist</c> already
/// applies at the whole-issue-type level, just scoped to one path within an otherwise-good type. Flagged
/// explicitly in the branch report as a deliberate, not-yet-solved gap.
/// </summary>
public interface ILadysKnightOutIssueInterface : IGameAbstraction
{
    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(Hero owner, out float difficultyMultiplier);

    void MirrorQuestAccepted(Hero owner, float difficultyMultiplier);
}

/// <inheritdoc cref="ILadysKnightOutIssueInterface"/>
public class LadysKnightOutIssueInterface : ILadysKnightOutIssueInterface
{
    private static readonly FieldInfo DifficultyMultiplierField =
        AccessTools.Field(typeof(LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest), "_difficultyMultiplier");

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not LadysKnightOutIssueBehavior.LadysKnightOutIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out float difficultyMultiplier)
    {
        difficultyMultiplier = 0f;

        if (owner?.Issue?.IssueQuest is not LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest quest) return false;

        difficultyMultiplier = (float)DifficultyMultiplierField.GetValue(quest);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, float difficultyMultiplier)
    {
        if (owner?.Issue is not LadysKnightOutIssueBehavior.LadysKnightOutIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest quest) return;

            DifficultyMultiplierField.SetValue(quest, difficultyMultiplier);
        }
    }
}
