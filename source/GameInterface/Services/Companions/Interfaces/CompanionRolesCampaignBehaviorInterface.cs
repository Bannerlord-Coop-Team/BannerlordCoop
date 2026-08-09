using Helpers;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Companions.Interfaces;

public interface ICompanionRolesCampaignBehaviorInterface : IGameAbstraction
{
    void PartyScreenClosed(IEnumerable<TroopRosterElement> leftMemberElements, IEnumerable<TroopRosterElement> leftPrisonerElements, PartyBase rightOwnerParty, bool fromCancel);
}

public class CompanionRolesCampaignBehaviorInterface : ICompanionRolesCampaignBehaviorInterface
{
    public void PartyScreenClosed(
        IEnumerable<TroopRosterElement> leftMemberElements,
        IEnumerable<TroopRosterElement> leftPrisonerElements,
        PartyBase rightOwnerParty,
        bool fromCancel)
    {
        if (fromCancel) return;

        CharacterObject character = leftMemberElements.FirstOrDefault(delegate (TroopRosterElement x)
        {
            Hero heroObject = x.Character.HeroObject;
            return heroObject != null && heroObject.CompanionOf != null;
        }).Character;

        EndCaptivityAction.ApplyByReleasedAfterBattle(character.HeroObject);
        character.HeroObject.ChangeState(Hero.CharacterStates.Active);
        rightOwnerParty.MobileParty.AddElementToMemberRoster(character, 1, false);

        int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
        if (character.HeroObject.Gold < partyGoldLowerThreshold)
        {
            GiveGoldAction.ApplyBetweenCharacters(rightOwnerParty.LeaderHero, character.HeroObject, partyGoldLowerThreshold - character.HeroObject.Gold, false);
        }
        MobileParty mobileParty = MobilePartyHelper.CreateNewClanMobileParty(character.HeroObject, rightOwnerParty.LeaderHero.Clan);
        foreach (TroopRosterElement memberElement in leftMemberElements)
        {
            if (memberElement.Character != character)
            {
                mobileParty.MemberRoster.Add(memberElement);
                rightOwnerParty.MemberRoster.AddToCounts(memberElement.Character, -memberElement.Number, false, -memberElement.WoundedNumber, -memberElement.Xp, true, -1);
            }
        }
        foreach (TroopRosterElement prisonerElement in leftPrisonerElements)
        {
            mobileParty.MemberRoster.Add(prisonerElement);
            rightOwnerParty.PrisonRoster.AddToCounts(prisonerElement.Character, -prisonerElement.Number, false, -prisonerElement.WoundedNumber, -prisonerElement.Xp, true, -1);
        }
    }
}
