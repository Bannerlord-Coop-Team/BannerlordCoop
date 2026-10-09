using GameInterface.Services.TroopRosters.Data;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IAlternativeSolutionTroopValidator
{
    TroopRosterElementData[] ToData(TroopRoster roster);

    // Whether the troops satisfy what the issue's alternative solution asks for
    bool AreValid(IssueBase issue, TroopRosterElementData[] troops);

    // Replaces the troops the issue has sent
    void Apply(IssueBase issue, TroopRosterElementData[] troops);

    // Gives the sent troops back to the local main party, used when the accept is turned down
    void ReturnToMainParty(IssueBase issue);

    void ReturnTroopsToParty(MobileParty party, TroopRosterElementData[] troops);
}
