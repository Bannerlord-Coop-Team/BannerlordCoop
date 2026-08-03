using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.LandLordNeedsManualLaborersAcceptancePatches"/>/
/// <see cref="Handlers.LandLordNeedsManualLaborersIssueHandler"/> need to capture and authoritatively
/// force-write a genuine accept's terms onto every other peer's mirrored
/// <see cref="LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest"/>.
///
/// The survey flagged only <c>_maximumPrisonerCount = MBRandom.RandomInt(40, 60)</c> (rolled in the Quest's own
/// ctor, i.e. at ACCEPT time) - correct, but it missed that <c>_requestedPrisonerCount</c> (the ctor PARAMETER,
/// supplied by <c>LandLordNeedsManualLaborersIssue.GenerateIssueQuest</c> from its own
/// <c>RequestedPrisonerCount</c> computed property) is ALSO genuinely per-client-divergent: that property reads
/// <c>base.IssueDifficultyMultiplier</c>, and <c>IssueBase.StartIssueWithQuest</c> (the real accept entry point)
/// unconditionally RE-ROLLS <c>_issueDifficultyMultiplier</c> fresh from each machine's own
/// <c>Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier()</c> immediately before calling
/// <c>GenerateIssueQuest</c> - the exact same per-client-at-accept-time mechanism Village Needs Crafting
/// Materials/Gang Leader Needs Recruits/Landlord Training for Retainers already needed their own force-write
/// for. Both fields are captured together here, off the newly-created Quest, right after a genuine accept.
///
/// The survey's OTHER flag ("a live PrisonerRansomValue model read at delivery time") is real too, but needs no
/// separate capture mechanism: <c>OnDoneClicked</c> (which performs that read) only ever runs inside the
/// party-screen flow opened from <c>quest_discuss</c>'s "Yes, I have brought you a few men." option, gated by
/// <c>CheckIfThereIsSuitablePrisonerInPlayer()</c> - see
/// <see cref="Patches.LandLordNeedsManualLaborersOwnershipGatePatches"/>, which gates that check to the real
/// owner only. Once gated, this live read only ever executes on the one machine whose own
/// <c>Hero.MainHero</c> is the correct beneficiary - no cross-peer divergence to correct.
/// </summary>
public interface ILandLordNeedsManualLaborersIssueInterface : IGameAbstraction
{
    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(Hero owner, out int requestedPrisonerCount, out int maximumPrisonerCount);

    void MirrorQuestAccepted(Hero owner, int requestedPrisonerCount, int maximumPrisonerCount);
}

/// <inheritdoc cref="ILandLordNeedsManualLaborersIssueInterface"/>
public class LandLordNeedsManualLaborersIssueInterface : ILandLordNeedsManualLaborersIssueInterface
{
    private static readonly FieldInfo RequestedPrisonerCountField =
        AccessTools.Field(typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest), "_requestedPrisonerCount");
    private static readonly FieldInfo MaximumPrisonerCountField =
        AccessTools.Field(typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest), "_maximumPrisonerCount");

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out int requestedPrisonerCount, out int maximumPrisonerCount)
    {
        requestedPrisonerCount = 0;
        maximumPrisonerCount = 0;

        if (owner?.Issue?.IssueQuest is not LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest quest) return false;

        requestedPrisonerCount = (int)RequestedPrisonerCountField.GetValue(quest);
        maximumPrisonerCount = (int)MaximumPrisonerCountField.GetValue(quest);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, int requestedPrisonerCount, int maximumPrisonerCount)
    {
        if (owner?.Issue is not LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssueQuest quest) return;

            RequestedPrisonerCountField.SetValue(quest, requestedPrisonerCount);
            MaximumPrisonerCountField.SetValue(quest, maximumPrisonerCount);
        }
    }
}
