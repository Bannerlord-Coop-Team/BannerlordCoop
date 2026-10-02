using Common;
using Common.Util;
using Helpers;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic;

internal interface IAlternativeSolutionTroopSelection
{
    IssueBase FindIssue(PartyScreenLogic logic);
    void KeepSelection(IssueBase issue, TroopRoster selected);
    void Rollback(Hero owner, bool closeScreen = true);
}

internal sealed class AlternativeSolutionTroopSelection : IAlternativeSolutionTroopSelection
{
    private IssueBase returnedSelection;

    public IssueBase FindIssue(PartyScreenLogic logic)
    {
        if (ModInformation.IsServer || logic == null ||
            Game.Current.GameStateManager.ActiveState is not PartyState state ||
            state.PartyScreenMode != PartyScreenHelper.PartyScreenMode.QuestTroopManage ||
            !ReferenceEquals(state.PartyScreenLogic, logic)) return null;

        return Campaign.Current.IssueManager.Issues.Values.FirstOrDefault(issue =>
            issue.IsOngoingWithoutQuest &&
            QuestTypeRegistry.Get(issue)?.SupportsAlternativeAccept == true &&
            ReferenceEquals(issue.AlternativeSolutionSentTroops, logic.CurrentData.LeftMemberRoster));
    }

    public void KeepSelection(IssueBase issue, TroopRoster selected)
    {
        using (new AllowedThread())
        {
            var sent = issue.AlternativeSolutionSentTroops;
            MobileParty.MainParty.MemberRoster.Add(sent);
            sent.Clear();
            sent.Add(selected);
        }
        // Done restored the party; this roster now records a claim, not a removal.
        returnedSelection = issue;
    }

    public void Rollback(Hero owner, bool closeScreen = true)
    {
        var issue = owner?.Issue;
        if (ModInformation.IsServer || issue?.IsOngoingWithoutQuest != true ||
            QuestTypeRegistry.Get(issue)?.SupportsAlternativeAccept != true) return;

        var state = Game.Current.GameStateManager.ActiveState as PartyState;
        var logic = state?.PartyScreenLogic;
        bool hasOpenSelection = ReferenceEquals(FindIssue(logic), issue);
        using (new AllowedThread())
        {
            if (hasOpenSelection && !ReferenceEquals(returnedSelection, issue)) logic.Reset(true);

            var sent = issue.AlternativeSolutionSentTroops;
            if (ReferenceEquals(returnedSelection, issue)) returnedSelection = null;
            else MobileParty.MainParty.MemberRoster.Add(sent);
            sent.Clear();

            if (hasOpenSelection)
            {
                // A queued screen callback must not reset the party or the accepted issue.
                var detached = new PartyScreenData();
                detached.InitializeCopyFrom(null, null);
                detached.CopyFromScreenData(logic.CurrentData);
                logic.CurrentData = detached;
                logic.MemberRosters[0] = detached.LeftMemberRoster;
                logic.MemberRosters[1] = detached.RightMemberRoster;
                logic.PrisonerRosters[0] = detached.LeftPrisonerRoster;
                logic.PrisonerRosters[1] = detached.RightPrisonerRoster;
            }
        }

        if (hasOpenSelection && closeScreen) PartyScreenHelper.CloseScreen(false, true);
    }
}
