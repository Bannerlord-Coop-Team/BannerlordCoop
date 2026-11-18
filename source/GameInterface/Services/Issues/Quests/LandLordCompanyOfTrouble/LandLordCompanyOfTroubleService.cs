using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
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
}

internal class LandLordCompanyOfTroubleService : ILandLordCompanyOfTroubleService
{
    private readonly IOutcomeDispatcher dispatcher;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IPlayerManager players;
    private readonly IObjectManager objectManager;

    public LandLordCompanyOfTroubleService(IOutcomeDispatcher dispatcher, IIssueOwnerResolver ownerResolver, IPlayerManager players, IObjectManager objectManager)
    {
        this.dispatcher = dispatcher;
        this.ownerResolver = ownerResolver;
        this.players = players;
        this.objectManager = objectManager;
    }

    public void RemoveCompanyOf(Quest quest)
    {
        // Already on the game thread, this only adds the catch so a failed cleanup doesn't throw into the vanilla code ending the quest
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

        // An owner without a party, like one held prisoner, has no mercenaries for vanilla's OnFinalize to count
        if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero) || ownerHero.PartyBelongedTo == null)
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

    private void OnMapEventEnded(Quest quest, MapEvent mapEvent)
    {
        if (MobileParty.MainParty != null)
        {
            EndAfterBattle(quest, mapEvent);
            return;
        }

        // Losing a battle takes the owner prisoner, which leaves them without a party, so the party they fought with stands in
        var owner = Hero.MainHero;
        if (!players.TryGetPlayer(owner, out var player) || !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var ownerParty))
        {
            return;
        }

        using (new MainHeroSubstitutionScope(owner, ownerParty))
        {
            EndAfterBattle(quest, mapEvent);
        }
    }

    private static void EndAfterBattle(Quest quest, MapEvent mapEvent)
    {
        var company = quest._companyOfTroubleParty;
        var involvedParties = mapEvent.InvolvedParties.ToList();
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
        LandLordCompanyOfTroubleFinalizationProofStrategy.FailAsCompanyLeft(quest);
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
