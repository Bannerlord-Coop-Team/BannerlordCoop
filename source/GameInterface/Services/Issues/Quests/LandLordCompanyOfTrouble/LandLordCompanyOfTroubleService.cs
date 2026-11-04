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
        var company = quest._companyOfTroubleParty;
        if (company == null)
        {
            return;
        }

        // Cleared first, otherwise destroying it reads as someone else killing the company and fails the quest being ended
        quest._companyOfTroubleParty = null;
        GameThread.RunSafe(() => RemoveCompany(company), context: nameof(LandLordCompanyOfTroubleService));
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
            quest._troubleCharacterObject = MBObjectManager.Instance.GetObject<CharacterObject>("company_of_trouble_character");
        }

        if (!ownerResolver.TryResolveOwnerHero(issue, out _))
        {
            RemoveLeftovers(quest);
            return;
        }

        dispatcher.RunAsOwners<Quest>(ownedQuest =>
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

    private static void OnMapEventEnded(Quest quest, MapEvent mapEvent)
    {
        var involvedParties = mapEvent.InvolvedParties.ToList();
        if (!involvedParties.Contains(PartyBase.MainParty))
        {
            return;
        }

        var company = quest._companyOfTroubleParty;
        if (company == null)
        {
            quest.OnMapEventEnded(mapEvent);
            return;
        }

        if (!involvedParties.Contains(company.Party))
        {
            return;
        }

        if (mapEvent.WinningSide == PartyBase.MainParty.Side)
        {
            quest.QuestSuccessWithPlayerDefeatedCompany();
        }
        else
        {
            quest.QuestFailWithPlayerDefeatedAgainstCompany();
        }
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

    private static void RemoveCompany(MobileParty company)
    {
        if (!company.IsActive)
        {
            return;
        }

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
            if (party != quest._companyOfTroubleParty && count > 0 && !otherQuestParties.Contains(party))
            {
                party.MemberRoster.AddToCounts(quest._troubleCharacterObject, -count);
            }
        }

        if (quest._companyOfTroubleParty != null)
        {
            RemoveCompany(quest._companyOfTroubleParty);
        }
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
