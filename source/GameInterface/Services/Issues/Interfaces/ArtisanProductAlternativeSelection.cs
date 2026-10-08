using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static TaleWorlds.CampaignSystem.Party.PartyScreenLogic;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

internal interface IArtisanProductAlternativeSelection
{
    void Prepare(Issue issue, ref PartyScreenLogicInitializationData data);
}

internal sealed class ArtisanProductAlternativeSelection : IArtisanProductAlternativeSelection
{
    private Issue issue;
    private PartyPresentationDoneButtonDelegate done;

    public void Prepare(Issue issue, ref PartyScreenLogicInitializationData data)
    {
        this.issue = issue;
        done = data.PartyPresentationDoneButtonDelegate;
        using (new AllowedThread())
        {
            data.LeftMemberRoster = Copy(data.LeftMemberRoster);
            data.RightMemberRoster = Copy(data.RightMemberRoster);
            data.LeftPrisonerRoster = Copy(data.LeftPrisonerRoster);
            data.RightPrisonerRoster = Copy(data.RightPrisonerRoster);
            // Vanilla removed the companion before opening the selection screen.
            data.RightOwnerParty.MemberRoster.Add(issue.AlternativeSolutionSentTroops);
            issue.AlternativeSolutionSentTroops.Clear();
        }
        data.IsTroopUpgradesDisabled = true;
        data.PartyPresentationDoneButtonDelegate = SelectTroops;
    }

    private bool SelectTroops(TroopRoster leftMembers, TroopRoster leftPrisoners,
        TroopRoster rightMembers, TroopRoster rightPrisoners, FlattenedTroopRoster taken,
        FlattenedTroopRoster released, bool forced, PartyBase leftParty, PartyBase rightParty)
    {
        if (!done(leftMembers, leftPrisoners, rightMembers, rightPrisoners, taken, released, forced, leftParty, rightParty))
            return false;
        if (issue.IssueOwner.Issue != issue || !issue.IsOngoingWithoutQuest) return true;

        // Done records a claim; authenticated acceptance alone commits troops and wages.
        using (new AllowedThread())
        {
            issue.AlternativeSolutionSentTroops.Clear();
            foreach (var element in leftMembers.GetTroopRoster())
                issue.AlternativeSolutionSentTroops.Add(element);
        }
        return true;
    }

    private static TroopRoster Copy(TroopRoster roster)
    {
        var copy = TroopRoster.CreateDummyTroopRoster();
        // CloneRosterData discards troop XP.
        foreach (var element in roster.GetTroopRoster()) copy.Add(element);
        return copy;
    }
}
