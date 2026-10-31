using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble;

/// <summary>
/// The server's side of how Company of Trouble ends and what it leaves behind. The patches only forward to it.
/// </summary>
internal interface ILandLordCompanyOfTroubleService
{
    // Server: removes the quest's hostile company after the quest finalized
    void RemoveCompanyOf(Quest quest);

    // Server: finalizes the quest as its owner, or clears what it left behind when nobody owns it
    void FinalizeIssue(IssueBase issue);

    // Server: ends an owner's quest after a battle with the hostile company or one that killed the last mercenary
    void OnMapEventEnded(MapEvent mapEvent);

    // Server: fails an owner's quest when someone other than the owner destroys the hostile company
    void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty);
}
