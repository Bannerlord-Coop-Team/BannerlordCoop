using Common;
using Common.Util;
using Helpers;
using System;
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
    void KeepSelection(PartyScreenLogic logic, IssueBase issue, TroopRoster selected, TroopRoster remaining);
    TroopRoster GetReservedTroops(PartyScreenLogic logic, TroopRoster authoritativeRoster);
    bool IsCommitPending(PartyScreenLogic logic);
    string BeginCommit(PartyScreenLogic logic);
    void CompleteCommit(string commitId, bool accepted);
    void Rollback(Hero owner, bool closeScreen = true);
}

internal sealed class AlternativeSolutionTroopSelection : IAlternativeSolutionTroopSelection
{
    private IssueBase returnedSelection;
    private PartyScreenLogic pendingLogic;
    private string pendingCommitId;

    public bool IsCommitPending(PartyScreenLogic logic)
        => pendingCommitId != null &&
           (ReferenceEquals(pendingLogic, logic) ||
            (logic?.RightOwnerParty != null && ReferenceEquals(pendingLogic.RightOwnerParty, logic.RightOwnerParty)));

    public string BeginCommit(PartyScreenLogic logic)
    {
        pendingLogic = logic;
        pendingCommitId = Guid.NewGuid().ToString("N");
        return pendingCommitId;
    }

    public void CompleteCommit(string commitId, bool accepted)
    {
        if (pendingCommitId == null || pendingCommitId != commitId) return;
        var logic = pendingLogic;
        pendingLogic = null;
        pendingCommitId = null;
        var issue = FindIssue(logic);
        if (issue != null && (!accepted || !RestoreSelection(logic, issue))) Rollback(issue.IssueOwner);
        else
        {
            var active = (Game.Current.GameStateManager.ActiveState as PartyState)?.PartyScreenLogic;
            if (active != null && ReferenceEquals(active.RightOwnerParty, logic.RightOwnerParty)) active.OnReset(false);
        }
    }

    public IssueBase FindIssue(PartyScreenLogic logic)
    {
        if (ModInformation.IsServer || logic == null ||
            Game.Current?.GameStateManager?.ActiveState is not PartyState state ||
            state.PartyScreenMode != PartyScreenHelper.PartyScreenMode.QuestTroopManage ||
            !ReferenceEquals(state.PartyScreenLogic, logic)) return null;

        return Campaign.Current.IssueManager.Issues.Values.FirstOrDefault(issue =>
            issue.IsOngoingWithoutQuest &&
            QuestTypeRegistry.Get(issue)?.SupportsAlternativeAccept == true &&
            ReferenceEquals(issue.AlternativeSolutionSentTroops, logic.CurrentData.LeftMemberRoster));
    }

    public void KeepSelection(PartyScreenLogic logic, IssueBase issue, TroopRoster selected, TroopRoster remaining)
    {
        using (new AllowedThread())
        {
            var sent = issue.AlternativeSolutionSentTroops;
            if (!ReferenceEquals(returnedSelection, issue)) MobileParty.MainParty.MemberRoster.Add(sent);
            sent.Clear();
            sent.Add(selected);
            logic.CurrentData.RightMemberRoster = remaining;
            logic.MemberRosters[1] = remaining;
        }
        // Done restored the party; this roster now records a claim, not a removal.
        returnedSelection = issue;
    }

    public TroopRoster GetReservedTroops(PartyScreenLogic logic, TroopRoster authoritativeRoster)
    {
        if (returnedSelection == null || logic == null ||
            !ReferenceEquals(logic.RightOwnerParty?.MemberRoster, authoritativeRoster) ||
            ReferenceEquals(logic.CurrentData.RightMemberRoster, authoritativeRoster)) return null;

        return ReferenceEquals(returnedSelection, FindIssue(logic)) ? logic._initialData.LeftMemberRoster : null;
    }

    private bool RestoreSelection(PartyScreenLogic logic, IssueBase issue)
    {
        var selected = issue.AlternativeSolutionSentTroops;
        var party = logic.RightOwnerParty;
        for (int i = 0; i < selected.Count; i++)
        {
            var troop = selected.GetElementCopyAtIndex(i);
            int index = party.MemberRoster.FindIndexOfTroop(troop.Character);
            if (index < 0 || party.MemberRoster.GetElementNumber(index) < troop.Number ||
                party.MemberRoster.GetElementWoundedNumber(index) < troop.WoundedNumber ||
                party.MemberRoster.GetElementNumber(index) - party.MemberRoster.GetElementWoundedNumber(index) <
                    troop.Number - troop.WoundedNumber) return false;
        }

        using (new AllowedThread())
        {
            var remaining = logic.CurrentData.RightMemberRoster;
            remaining.Clear();
            // XP-only updates do not invalidate the native roster's cached list.
            for (int i = 0; i < party.MemberRoster.Count; i++)
                remaining.Add(party.MemberRoster.GetElementCopyAtIndex(i));
            for (int i = 0; i < selected.Count; i++)
            {
                var troop = selected.GetElementCopyAtIndex(i);
                int index = remaining.FindIndexOfTroop(troop.Character);
                var element = remaining.GetElementCopyAtIndex(index);
                int availableXp = element.Xp;
                element.Number -= troop.Number;
                element.Xp = Math.Max(0, availableXp - troop.Xp);
                party.OnXpChanged(remaining, ref element);
                selected.SetElementXp(selected.FindIndexOfTroop(troop.Character), availableXp - element.Xp);
                remaining.AddToCounts(troop.Character, -troop.Number, false, -troop.WoundedNumber,
                    element.Xp - availableXp);
            }
            selected.UpdateVersion();
            logic._initialData.CopyFromScreenData(logic.CurrentData);
            if (logic._savedData != null) logic.SavePartyScreenData();
        }
        return true;
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
            if (hasOpenSelection) logic.Reset(true);

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
