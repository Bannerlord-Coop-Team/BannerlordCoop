using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

/// <summary>
/// Finds the Company of Trouble mercenaries outside the local main party.
/// </summary>
internal interface ILandLordCompanyOfTroubleMercenaries
{
    // Whether a party other than the local main party carries company mercenaries, a hostile company or another player's
    bool CarriesCompanyMercenaries(MobileParty party);
}

internal class LandLordCompanyOfTroubleMercenaries : ILandLordCompanyOfTroubleMercenaries
{
    public bool CarriesCompanyMercenaries(MobileParty party)
    {
        if (party == null || party == MobileParty.MainParty)
        {
            return false;
        }

        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            if (element.Character.StringId == Quest.TroubleCharacterObjectStringId)
            {
                return true;
            }
        }

        return false;
    }
}
