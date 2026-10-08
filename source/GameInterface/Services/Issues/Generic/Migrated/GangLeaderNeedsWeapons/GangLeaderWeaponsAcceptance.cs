using Common.Util;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Generic.Dispatch;
using Helpers;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

internal interface IGangLeaderWeaponsAcceptance :
    ICreationCaptureStrategy<Issue, GangLeaderWeaponsCreationFields>,
    IRaceArbitratedAcceptMirrorStrategy<GangLeaderWeaponsQuestFields>,
    IAlternativeAcceptMirrorStrategy<GangLeaderWeaponsAlternativeFields>
{
    new void RejectAcceptance(Hero owner);
    bool TryGetAlternativeSelection(PartyScreenLogic logic, out Issue issue);
    void FinishAlternativeSelection(Issue issue, PartyScreenLogic logic, TroopRoster selectedTroops);
    bool RestoreAlternativeSelection(Hero owner, bool closeScreen);
    void CancelAlternativeSelection(Hero owner);
}

internal sealed class GangLeaderWeaponsAcceptance : IGangLeaderWeaponsAcceptance
{
    private Issue restoredSelection;

    public void CancelAlternativeSelection(Hero owner)
    {
        if (owner?.Issue?.IsOngoingWithoutQuest == true &&
            RestoreAlternativeSelection(owner, closeScreen: true) && Hero.OneToOneConversationHero == owner)
            Campaign.Current.ConversationManager.EndConversation();
    }

    public bool TryGetAlternativeSelection(PartyScreenLogic logic, out Issue issue)
    {
        issue = Hero.OneToOneConversationHero?.Issue as Issue;
        return issue?.IsOngoingWithoutQuest == true &&
            ReferenceEquals(logic.CurrentData.LeftMemberRoster, issue.AlternativeSolutionSentTroops);
    }

    public void FinishAlternativeSelection(Issue issue, PartyScreenLogic logic, TroopRoster selectedTroops)
    {
        // Done restored the regular troops; the companion was removed before the screen opened.
        MobileParty.MainParty.MemberRoster.Add(logic._initialData.LeftMemberRoster);
        issue.AlternativeSolutionSentTroops.Clear();
        issue.AlternativeSolutionSentTroops.Add(selectedTroops);
        logic.MemberRosters[0] = issue.AlternativeSolutionSentTroops;
        restoredSelection = issue;
    }

    public bool RestoreAlternativeSelection(Hero owner, bool closeScreen)
    {
        if (owner?.Issue is not Issue issue) return false;
        if (!issue.IsOngoingWithoutQuest) return true;
        if (issue.AlternativeSolutionSentTroops.TotalManCount == 0) return true;
        var logic = (Game.Current?.GameStateManager?.ActiveState as PartyState)?.PartyScreenLogic;
        var hasScreen = logic != null &&
            ReferenceEquals(logic.CurrentData.LeftMemberRoster, issue.AlternativeSolutionSentTroops);
        using (new AllowedThread())
        {
            if (restoredSelection != issue)
            {
                if (hasScreen) logic.Reset(true);
                MobileParty.MainParty.MemberRoster.Add(issue.AlternativeSolutionSentTroops);
            }
            issue.AlternativeSolutionSentTroops.Clear();
        }
        if (restoredSelection == issue) restoredSelection = null;
        if (hasScreen && closeScreen)
        {
            PartyScreenHelper.CloseScreen(false, fromCancel: true);
        }
        return true;
    }

    public bool TryCaptureFields(Issue issue, out GangLeaderWeaponsCreationFields fields)
    {
        fields = default;
        if (issue == null) return false;
        fields = new GangLeaderWeaponsCreationFields(
            issue._requiredWeaponClassIndex, issue._averagePriceForItem, issue.IssueDueTime);
        return true;
    }

    public Issue ConstructReplicated(Hero owner, GangLeaderWeaponsCreationFields fields)
    {
        return new Issue(owner)
        {
            _requiredWeaponClassIndex = fields.WeaponClassIndex,
            _averagePriceForItem = fields.AveragePrice,
            IssueDueTime = fields.DueTime,
        };
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (!issue.CheckPreconditions(owner, out _)) return;

        using (new IssueDispatchReplayGuard())
        {
            issue.StartIssueWithQuest();
            if (issue.IssueQuest is Quest quest && quest._playerStartsQuestLog == null)
            {
                quest.QuestAcceptedConsequences();
            }
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out GangLeaderWeaponsQuestFields fields)
    {
        fields = default;
        if (owner?.Issue is not Issue issue || issue.IssueQuest is not Quest quest) return false;

        fields = new GangLeaderWeaponsQuestFields(
            quest.StringId, quest.QuestDueTime, quest.RewardGold, quest._randomForRequiredWeaponClass,
            quest._requestedWeaponAmount, quest._issueDifficulty, issue._averagePriceForItem,
            quest._collectedItemAmount);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, GangLeaderWeaponsQuestFields fields)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            issue._issueDifficultyMultiplier = fields.Difficulty;
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            issue.IsTriedToSolveBefore = true;
            issue.IssueDueTime = CampaignTime.Never;
            var quest = new Quest(fields.QuestId, owner, fields.DueTime, fields.RewardGold,
                fields.WeaponClassIndex, fields.Amount, fields.Difficulty, fields.AveragePrice);
            issue.IssueQuest = quest;
            using (new GangLeaderWeaponsActionScope(quest)) quest.StartQuest();
            // Observers must not initialize another player's progress from their inventory.
            quest._collectedItemAmount = fields.CollectedAmount;
            quest._playerStartsQuestLog = quest.AddDiscreteLog(quest.PlayerStartsQuestLogText,
                new TextObject("{=9j7LJk60}Collected Items"), fields.CollectedAmount, fields.Amount);
        }
    }

    public void ReplayAlternativeAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (!issue.CheckPreconditions(owner, out _)) return;
        using (new IssueDispatchReplayGuard())
        {
            issue.StartIssueWithAlternativeSolution();
        }
    }

    public bool TryCaptureAlternativeFields(Hero owner, out GangLeaderWeaponsAlternativeFields fields)
    {
        fields = default;
        if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return false;
        fields = new GangLeaderWeaponsAlternativeFields(issue._issueDifficultyMultiplier,
            AlternativeSolutionVanillaStateSync.Capture(issue));
        return true;
    }

    public void MirrorAlternativeAccepted(Hero owner, GangLeaderWeaponsAlternativeFields fields)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        using (new AllowedThread())
        {
            issue._issueDifficultyMultiplier = fields.Difficulty;
            issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            issue.IsTriedToSolveBefore = true;
            AlternativeSolutionVanillaStateSync.Apply(issue, fields.State);
        }
    }

    public void RejectAcceptance(Hero owner)
    {
        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) &&
            ownership.TryGetOwnerControllerId(owner, out _)) return;
        CancelAlternativeSelection(owner);
        AcceptMirrorSupport.RejectAcceptance(owner);
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct GangLeaderWeaponsCreationFields
{
    [ProtoMember(1)] public readonly int WeaponClassIndex;
    [ProtoMember(2)] public readonly int AveragePrice;
    [ProtoMember(3)] public readonly CampaignTime DueTime;

    public GangLeaderWeaponsCreationFields(int weaponClassIndex, int averagePrice, CampaignTime dueTime)
    {
        WeaponClassIndex = weaponClassIndex;
        AveragePrice = averagePrice;
        DueTime = dueTime;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct GangLeaderWeaponsQuestFields
{
    [ProtoMember(1)] public readonly string QuestId;
    [ProtoMember(2)] public readonly CampaignTime DueTime;
    [ProtoMember(3)] public readonly int RewardGold;
    [ProtoMember(4)] public readonly int WeaponClassIndex;
    [ProtoMember(5)] public readonly int Amount;
    [ProtoMember(6)] public readonly float Difficulty;
    [ProtoMember(7)] public readonly int AveragePrice;
    [ProtoMember(8)] public readonly int CollectedAmount;

    public GangLeaderWeaponsQuestFields(string questId, CampaignTime dueTime, int rewardGold,
        int weaponClassIndex, int amount, float difficulty, int averagePrice, int collectedAmount)
    {
        QuestId = questId;
        DueTime = dueTime;
        RewardGold = rewardGold;
        WeaponClassIndex = weaponClassIndex;
        Amount = amount;
        Difficulty = difficulty;
        AveragePrice = averagePrice;
        CollectedAmount = collectedAmount;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct GangLeaderWeaponsAlternativeFields
{
    [ProtoMember(1)] public readonly float Difficulty;
    [ProtoMember(2)] public readonly AlternativeSolutionVanillaState State;

    public GangLeaderWeaponsAlternativeFields(float difficulty, AlternativeSolutionVanillaState state)
    {
        Difficulty = difficulty;
        State = state;
    }
}
