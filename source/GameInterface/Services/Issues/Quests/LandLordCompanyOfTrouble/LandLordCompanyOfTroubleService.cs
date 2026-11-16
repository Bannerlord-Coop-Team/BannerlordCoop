using Common;
using GameInterface.Services.Issues.Framework.Interface;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;
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

    // Owner's client: whether a party is a hostile company, which only the server knows as the quest's party
    bool IsHostileCompany(MobileParty party);
}

internal class LandLordCompanyOfTroubleService : ILandLordCompanyOfTroubleService
{
    private readonly IOutcomeDispatcher dispatcher;
    private readonly IIssueOwnerResolver ownerResolver;

    public LandLordCompanyOfTroubleService(IOutcomeDispatcher dispatcher, IIssueOwnerResolver ownerResolver)
    {
        this.dispatcher = dispatcher;
        this.ownerResolver = ownerResolver;
    }

    public void RemoveCompanyOf(Quest quest)
    {
        GameThread.RunSafe(() => RemoveCompany(quest), context: nameof(LandLordCompanyOfTroubleService));
    }

    public void FinalizeIssue(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest) || !quest.IsOngoing)
        {
            return;
        }

        // The field isn't saved and the server never starts the quest, so a loaded save leaves it null
        if (quest._troubleCharacterObject == null)
        {
            quest._troubleCharacterObject = MBObjectManager.Instance.GetObject<CharacterObject>(Quest.TroubleCharacterObjectStringId);
        }

        if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
        {
            RemoveLeftovers(quest);
            return;
        }

        dispatcher.RunAsOwner<Quest>(ownerHero, ownedQuest =>
        {
            if (ownedQuest == quest)
            {
                ownedQuest.OnFinalize();
            }
        });
    }

    public void OnMapEventEnded(MapEvent mapEvent)
    {
        dispatcher.RunAsOwners<Quest>(quest => OnMapEventEnded(quest, mapEvent));
    }

    public void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty)
    {
        if (!mobileParty.IsCurrentlyUsedByAQuest)
        {
            return;
        }

        dispatcher.RunAsOwners<Quest>(quest => OnPartyDestroyed(quest, mobileParty, destroyerParty));
    }

    public bool IsHostileCompany(MobileParty party)
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

    private static void OnMapEventEnded(Quest quest, MapEvent mapEvent)
    {
        var involvedParties = mapEvent.InvolvedParties.ToList();
        var company = quest._companyOfTroubleParty;
        var end = ClassifyBattleEnd(
            ownerInvolved: involvedParties.Contains(PartyBase.MainParty),
            companyExists: company != null,
            companyInvolved: company != null && involvedParties.Contains(company.Party),
            ownerWon: mapEvent.WinningSide == PartyBase.MainParty.Side);

        switch (end)
        {
            case LandLordCompanyOfTroubleBattleEnd.MercenariesInOwnersParty:
                quest.OnMapEventEnded(mapEvent);
                break;
            case LandLordCompanyOfTroubleBattleEnd.OwnerWon:
                quest.QuestSuccessWithPlayerDefeatedCompany();
                break;
            case LandLordCompanyOfTroubleBattleEnd.OwnerLost:
                quest.QuestFailWithPlayerDefeatedAgainstCompany();
                break;
        }
    }

    internal static LandLordCompanyOfTroubleBattleEnd ClassifyBattleEnd(bool ownerInvolved, bool companyExists, bool companyInvolved, bool ownerWon)
    {
        if (!ownerInvolved)
        {
            return LandLordCompanyOfTroubleBattleEnd.Ignored;
        }

        // Before "So be it!" the mercenaries fight in the owner's party, and vanilla checks whether they all died
        if (!companyExists)
        {
            return LandLordCompanyOfTroubleBattleEnd.MercenariesInOwnersParty;
        }

        if (!companyInvolved)
        {
            return LandLordCompanyOfTroubleBattleEnd.Ignored;
        }

        return ownerWon ? LandLordCompanyOfTroubleBattleEnd.OwnerWon : LandLordCompanyOfTroubleBattleEnd.OwnerLost;
    }

    private static void OnPartyDestroyed(Quest quest, MobileParty mobileParty, PartyBase destroyerParty)
    {
        if (quest._companyOfTroubleParty != mobileParty || destroyerParty == PartyBase.MainParty)
        {
            return;
        }

        // DestroyPartyAction is still running for this party, so the finalize cleanup must not destroy it again
        quest._companyOfTroubleParty = null;
        quest.RelationshipChangeWithQuestGiver = -2;
        quest.AddLog(quest.QuestFailCompanyLeft);
        quest.CompleteQuestWithFail();
    }

    private static void RemoveCompany(Quest quest)
    {
        var company = quest._companyOfTroubleParty;
        if (company == null)
        {
            return;
        }

        // Cleared first, otherwise destroying it reads as someone else killing the company and fails the quest being ended
        quest._companyOfTroubleParty = null;

        if (!company.IsActive)
        {
            return;
        }

        // A party can't be destroyed in the middle of a battle, so it is handed back to the AI as an ordinary bandit party
        if (company.MapEvent != null)
        {
            company.Ai.SetDoNotMakeNewDecisions(false);
            company.SetPartyUsedByQuest(false);
            return;
        }

        DestroyPartyAction.Apply(null, company);
    }

    private void RemoveLeftovers(Quest quest)
    {
        // Every company quest uses the same character, so another player's mercenaries must be left alone
        var otherQuestParties = OtherQuestParties(quest);

        foreach (var party in MobileParty.All.ToList())
        {
            var count = party.MemberRoster.GetTroopCount(quest._troubleCharacterObject);
            if (count == 0)
            {
                continue;
            }

            if (party == quest._companyOfTroubleParty || otherQuestParties.Contains(party))
            {
                continue;
            }

            party.MemberRoster.AddToCounts(quest._troubleCharacterObject, -count);
        }

        RemoveCompany(quest);
    }

    private HashSet<MobileParty> OtherQuestParties(Quest quest)
    {
        var parties = new HashSet<MobileParty>();

        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            if (!(issue.IssueQuest is Quest other) || other == quest || !other.IsOngoing)
            {
                continue;
            }

            if (other._companyOfTroubleParty != null)
            {
                parties.Add(other._companyOfTroubleParty);
            }

            if (ownerResolver.TryResolveOwnerHero(issue, out var ownerHero) && ownerHero.PartyBelongedTo != null)
            {
                parties.Add(ownerHero.PartyBelongedTo);
            }
        }

        return parties;
    }
}
