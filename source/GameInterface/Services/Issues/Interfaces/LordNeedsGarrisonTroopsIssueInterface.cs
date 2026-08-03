using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.LordNeedsGarrisonTroopsIssueCreationPatch"/>/
/// <see cref="Patches.LordNeedsGarrisonTroopsAcceptancePatches"/>/
/// <see cref="Handlers.LordNeedsGarrisonTroopsIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue"/>.
///
/// TWO genuinely separate rolls exist, at two different times - the survey only flagged the second one:
///
/// 1. CREATION time: the Issue's own ctor calls <c>CharacterHelper.GetTroopTree(...).GetRandomElementInefficiently()</c>
///    to pick <c>_neededTroopType</c> - a real creation-time dice roll the survey missed entirely (same shape
///    as Village Needs Draught Animals'/Lord Needs Horses' equivalents). <c>_settlement</c> is a plain ctor
///    parameter (not rolled) - already deterministic, since <c>LordNeedsGarrisonTroopsIssueQuestBehavior.ConditionsHold</c>
///    only ever runs from the server-gated ambient generation path (see
///    <see cref="Patches.IssuesCampaignBehaviorGenerationPatches"/>) - so it's passed straight through to
///    <see cref="ConstructReplicated"/> rather than force-written via reflection.
///
/// 2. ACCEPT time: the survey's flagged concern - <c>RewardGold</c> reads a LIVE
///    <c>Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(_neededTroopType, Hero.MainHero)</c>
///    (genuinely per-client: each peer's own <c>Hero.MainHero</c>) - is correct, and it also missed that
///    <c>NumberOfTroopToBeRecruited</c> (from <c>base.IssueDifficultyMultiplier</c>) is re-derived at that same
///    moment. Both are read by <c>GenerateIssueQuest</c> at ACCEPT time (after <c>IssueBase.StartIssueWithQuest</c>
///    re-rolls <c>_issueDifficultyMultiplier</c> fresh) and baked into the Quest's own
///    <c>_requestedTroopAmount</c>/<c>_rewardGold</c> fields - captured/force-written here, same shape as
///    Village Needs Crafting Materials.
///
/// Forcing <c>_neededTroopType</c> at creation time alone would NOT fix the reward divergence: even with an
/// identical troop type everywhere, each peer's own <c>Hero.MainHero</c> still differs, so the live
/// recruitment-cost model can still price it differently - hence the SEPARATE accept-time force-write below.
/// </summary>
public interface ILordNeedsGarrisonTroopsIssueInterface : IGameAbstraction
{
    bool TryCaptureFields(
        LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue issue,
        out Settlement settlement,
        out CharacterObject neededTroopType);

    LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue ConstructReplicated(
        Hero owner, Settlement settlement, CharacterObject neededTroopType);

    void RegisterReplicated(Hero owner, LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue issue);

    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(Hero owner, out int requestedTroopAmount, out int rewardGold);

    void MirrorQuestAccepted(Hero owner, int requestedTroopAmount, int rewardGold);
}

/// <inheritdoc cref="ILordNeedsGarrisonTroopsIssueInterface"/>
public class LordNeedsGarrisonTroopsIssueInterface : ILordNeedsGarrisonTroopsIssueInterface
{
    private static readonly FieldInfo NeededTroopTypeField =
        AccessTools.Field(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue), "_neededTroopType");
    private static readonly FieldInfo SettlementField =
        AccessTools.Field(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue), "_settlement");
    private static readonly FieldInfo RequestedTroopAmountField =
        AccessTools.Field(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest), "_requestedTroopAmount");
    private static readonly FieldInfo RewardGoldField =
        AccessTools.Field(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest), "_rewardGold");
    // The quest's own _rewardGold (above) is what PlayerTransferredTroopsToGarrisonCommander actually pays out
    // and is the one that matters for gameplay - but QuestBase's separate public readonly RewardGold field
    // (set from the same ctor parameter) is what the native quest-journal UI displays, and would otherwise
    // stay stale/uncorrected on a mirroring peer whose own bare replay independently mis-derived it. Forced
    // alongside _rewardGold for the same reason Village Needs Crafting Materials' equivalent forces it.
    private static readonly FieldInfo BaseRewardGoldField =
        AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

    public bool TryCaptureFields(
        LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue issue,
        out Settlement settlement,
        out CharacterObject neededTroopType)
    {
        settlement = null;
        neededTroopType = null;
        if (issue == null) return false;

        settlement = (Settlement)SettlementField.GetValue(issue);
        neededTroopType = (CharacterObject)NeededTroopTypeField.GetValue(issue);
        return settlement != null && neededTroopType != null;
    }

    public LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue ConstructReplicated(
        Hero owner, Settlement settlement, CharacterObject neededTroopType)
    {
        // The public ctor is the only way to build one, and it independently re-rolls _neededTroopType via
        // GetRandomElementInefficiently(). _settlement is already deterministic (a plain ctor parameter, not
        // rolled - see the type doc comment), so it's passed straight through; only _neededTroopType needs
        // force-writing afterward.
        var issue = new LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue(owner, settlement);

        NeededTroopTypeField.SetValue(issue, neededTroopType);

        return issue;
    }

    public void RegisterReplicated(Hero owner, LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out int requestedTroopAmount, out int rewardGold)
    {
        requestedTroopAmount = 0;
        rewardGold = 0;

        if (owner?.Issue?.IssueQuest is not LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest quest) return false;

        requestedTroopAmount = (int)RequestedTroopAmountField.GetValue(quest);
        rewardGold = (int)RewardGoldField.GetValue(quest);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, int requestedTroopAmount, int rewardGold)
    {
        if (owner?.Issue is not LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest quest) return;

            RequestedTroopAmountField.SetValue(quest, requestedTroopAmount);
            RewardGoldField.SetValue(quest, rewardGold);
            BaseRewardGoldField.SetValue(quest, rewardGold);
        }
    }
}
