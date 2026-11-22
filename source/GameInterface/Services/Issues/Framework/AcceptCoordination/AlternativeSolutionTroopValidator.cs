using GameInterface.Services.Alleys;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// For validating troops being used as an alternate solution
/// </summary>
internal class AlternativeSolutionTroopValidator : IAlternativeSolutionTroopValidator
{
    private readonly IAlleyGarrisonData rosterData;

    public AlternativeSolutionTroopValidator(IAlleyGarrisonData rosterData)
    {
        this.rosterData = rosterData;
    }

    public TroopRosterElementData[] ToData(TroopRoster roster) => rosterData.ToData(roster);

    public bool AreValid(IssueBase issue, TroopRosterElementData[] troops)
    {
        return issue.DoTroopsSatisfyAlternativeSolution(rosterData.FromData(troops), out _);
    }

    public void Apply(IssueBase issue, TroopRosterElementData[] troops)
    {
        issue.AlternativeSolutionSentTroops.Clear();
        issue.AlternativeSolutionSentTroops.Add(rosterData.FromData(troops));
    }

    public void ReturnToMainParty(IssueBase issue)
    {
        var memberRoster = MobileParty.MainParty.MemberRoster;

        foreach (var element in issue.AlternativeSolutionSentTroops.GetTroopRoster())
        {
            if (element.Character.IsHero && !memberRoster.Contains(element.Character))
            {
                memberRoster.AddToCounts(element.Character, 1);
            }
        }

        issue.AlternativeSolutionSentTroops.Clear();
    }

    public void ReturnTroopsToParty(MobileParty party, TroopRosterElementData[] troops)
    {
        foreach (var element in rosterData.FromData(troops).GetTroopRoster())
        {
            if (!element.Character.IsHero)
            {
                party.MemberRoster.Add(element);
            }
        }
    }
}
