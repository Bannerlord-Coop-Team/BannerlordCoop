using Common.Messaging;
using Common.Util;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LordsNeedsTutor;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace E2E.Tests.Services.Issues;

public sealed partial class LordsNeedsTutorTests
{
    private QuestScenario CreateAnotherQuest()
    {
        var secondGiver = environment.CreateRegisteredObject<Hero>();
        var secondPupil = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = Get<Hero>(instance, secondGiver);
                    var pupil = Get<Hero>(instance, secondPupil);
                    giver.Clan.SetLeader(giver);
                    pupil.Clan = giver.Clan;
                    pupil.SetBirthDay(CampaignTime.YearsFromNow(-18));
                    pupil.StayingInSettlement = Get<Settlement>(instance, settlementId);
                    giver.StayingInSettlement = pupil.StayingInSettlement;
                }
            });
        CreateIssue(secondGiver, secondPupil);
        return new QuestScenario(environment, secondGiver);
    }

    private QuestScenario AcceptObserversQuest()
    {
        var second = CreateAnotherQuest();
        second.Accept(Observer);
        Observer.Call(() => ((Quest)second.QuestOf(Observer)).QuestAcceptedConsequences());
        return second;
    }

    private void KillPupil() => Kill(pupilId);

    private void Kill(string heroId, int? killerPlayer = null)
    {
        var method = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
        var shim = Assert.Single(Harmony.GetPatchInfo(method).Prefixes, patch => patch.owner == "Coop.Testing");
        var bootstrap = new Harmony(shim.owner);
        bootstrap.Unpatch(method, shim.PatchMethod);
        try
        {
            Server.Call(() =>
            {
                // An execution from a party screen runs on the server as the executing player
                using var scope = killerPlayer is int i
                    ? new MainHeroSubstitutionScope(Get<Hero>(Server, heroIds[i]), Get<MobileParty>(Server, partyIds[i]))
                    : null;
                KillCharacterAction.ApplyByMurder(Get<Hero>(Server, heroId), showNotification: false);
            });
        }
        finally
        {
            bootstrap.Patch(method, prefix: new HarmonyMethod(shim.PatchMethod));
        }
        Server.Call(() => Assert.False(Get<Hero>(Server, heroId).IsAlive));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GiverDeathCancelsOnlyItsQuestAndReturnsThePupil(bool ownershipLost, bool observerKills)
    {
        Accept();
        var second = AcceptObserversQuest();
        // A clan leader's death can make the pupil the new leader, which is the separate leader flow.
        var leaderId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = Get<Hero>(instance, giverId);
                    var leader = Get<Hero>(instance, leaderId);
                    leader.Clan = giver.Clan;
                    giver.Clan.SetLeader(leader);
                }
            });
        // Only the ownership entry is dropped: a real restart also dismisses every active quest, covered by DismissalCleansUpThePupilWhenOwnerOrServerLeaves
        if (ownershipLost)
            Server.Call(() =>
            {
                Server.Resolve<IIssueOwnershipRegistry>().Remove(giverId);
            });
        Kill(giverId, observerKills ? 1 : null);
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        var journal = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTutorJournal>());
        Assert.Contains(journal.Entries, entry => entry.Text.ToString().Contains("died"));
        var received = Assert.Single(Owner.InternalMessages.GetMessages<NetworkTutorJournal>());
        Assert.Contains(received.Entries, entry => entry.Text.ToString().Contains("died"));
        Assert.Empty(Observer.InternalMessages.GetMessages<NetworkTutorJournal>());
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var pupil = Get<Hero>(instance, pupilId);
                Assert.Null(Get<Hero>(instance, giverId).Issue);
                Assert.Null(pupil.CompanionOf);
                Assert.Same(Get<Hero>(instance, giverId).Clan, pupil.Clan);
                Assert.Equal(0, Get<MobileParty>(instance, partyIds[0]).MemberRoster.GetTroopCount(pupil.CharacterObject));
                foreach (var skill in Skills.All) Assert.Equal(originalFocus[skill.StringId], pupil.HeroDeveloper.GetFocus(skill));
                if (instance == Owner)
                    Assert.Null(Get<Hero>(instance, second.NotableId).Issue.IssueQuest);
                else
                    Assert.True(second.QuestOf(instance).IsOngoing);
            });
    }

    [Theory]
    [InlineData("war", IssueOutcome.QuestCancel)]
    [InlineData("hostility", IssueOutcome.QuestFail)]
    [InlineData("faction", IssueOutcome.QuestCancel)]
    [InlineData("marriage", IssueOutcome.QuestCancel)]
    [InlineData("companion-limit", IssueOutcome.QuestCancel)]
    [InlineData("perk-reset", IssueOutcome.QuestCancel)]
    [InlineData("succession", IssueOutcome.QuestCancel)]
    public void WorldOutcomesAffectOnlyTheirOwner(string trigger, IssueOutcome expected)
    {
        Accept();
        var second = AcceptObserversQuest();
        if (trigger == "marriage")
            foreach (var instance in Instances)
                instance.Call(() =>
                {
                    using (new AllowedThread())
                        Get<Hero>(instance, pupilId).Clan = Get<Hero>(instance, heroIds[0]).Clan;
                });
        Server.Call(() =>
        {
            var owner = Get<Hero>(Server, heroIds[0]);
            var pupil = Get<Hero>(Server, pupilId);
            var giver = Get<Hero>(Server, giverId);
            switch (trigger)
            {
                case "war":
                case "hostility":
                    DeclareWarAction.ApplyInternal(giver.MapFaction, owner.MapFaction,
                        trigger == "war" ? DeclareWarAction.DeclareWarDetail.CausedByRebellion : DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
                    break;
                case "faction":
                    FactionManager.DeclareWar(giver.MapFaction, owner.MapFaction);
                    CampaignEventDispatcher.Instance.OnClanChangedKingdom(owner.Clan, null, null, default, false);
                    break;
                case "marriage":
                    CampaignEventDispatcher.Instance.OnBeforeHeroesMarried(owner, pupil, false);
                    break;
                case "companion-limit":
                case "perk-reset":
                    // Prepare an over-limit cache without emitting the event being tested.
                    while (owner.Clan.Companions.Count <= owner.Clan.CompanionLimit) owner.Clan._companionsCache.Add(owner);
                    if (trigger == "companion-limit") CampaignEventDispatcher.Instance.OnNewCompanionAdded(owner);
                    else CampaignEventDispatcher.Instance.OnPerkReset(owner, DefaultPerks.Charm.NaturalLeader);
                    break;
                case "succession":
                    Server.Resolve<IMessageBroker>().Publish(this, new PlayerHeirSelectionCompleted(owner));
                    break;
            }
        });
        environment.FlushCoalescer();
        Assert.Equal(expected, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var pupil = Get<Hero>(instance, pupilId);
                Assert.Null(Get<Hero>(instance, giverId).Issue);
                Assert.Null(pupil.CompanionOf);
                if (trigger == "marriage") Assert.Same(Get<Hero>(instance, heroIds[0]).Clan, pupil.Clan);
                else Assert.Same(Get<Hero>(instance, giverId).Clan, pupil.Clan);
                Assert.Equal(0, Get<MobileParty>(instance, partyIds[0]).MemberRoster.GetTroopCount(pupil.CharacterObject));
                foreach (var skill in Skills.All) Assert.Equal(originalFocus[skill.StringId], pupil.HeroDeveloper.GetFocus(skill));
                Assert.NotNull(Get<Hero>(instance, second.NotableId).Issue);
                if (instance != Owner) Assert.True(Get<Hero>(instance, second.NotableId).Issue.IssueQuest.IsOngoing);
            });
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.IsForcingSupplies, true)]
    [InlineData(MapEvent.BattleTypes.IsForcingVolunteers, true)]
    [InlineData(MapEvent.BattleTypes.IsForcingSupplies, false)]
    public void CoercionOfTheGiversVillageFailsOnlyTheActingOwnersQuest(MapEvent.BattleTypes type, bool related)
    {
        Accept();
        var second = AcceptObserversQuest();
        var villageId = environment.CreateRegisteredObject<Village>();
        var boundId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var unrelatedClanId = environment.CreateRegisteredObject<Clan>();
        var power = 0f;
        var relation = 0f;
        var otherRelation = 0f;
        Server.Call(() =>
        {
            var giver = Get<Hero>(Server, giverId);
            var owner = Get<Hero>(Server, heroIds[0]);
            var observer = Get<Hero>(Server, heroIds[1]);
            var villageSettlement = Get<Settlement>(Server, settlementId);
            using (new AllowedThread())
            {
                var village = Get<Village>(Server, villageId);
                var bound = Get<Settlement>(Server, boundId);
                var town = Get<Town>(Server, townId);
                bound.SetSettlementComponent(town);
                town.OwnerClan = related ? giver.Clan : Get<Clan>(Server, unrelatedClanId);
                villageSettlement.SetSettlementComponent(village);
                villageSettlement.Village = village;
                village.Bound = bound;
                if (!giver.Clan.AliveLords.Contains(giver)) giver.Clan._aliveLordsCache.Add(giver);
                giver._power = 50f;
            }
            power = giver.Power;
            relation = owner.GetRelation(giver);
            otherRelation = observer.GetRelation(giver);
            MapEvent map;
            using (new AllowedThread())
            {
                map = new MapEvent();
                map._mapEventType = type;
                map.MapEventSettlement = villageSettlement;
            }
            CampaignEventDispatcher.Instance.OnMapEventStarted(map, Get<MobileParty>(Server, partyIds[0]).Party, villageSettlement.Party);
            Assert.Equal(related ? power - 10 : power, giver.Power);
            Assert.Equal(related ? relation - 5 : relation, owner.GetRelation(giver));
            Assert.Equal(otherRelation, observer.GetRelation(giver));
        });
        environment.FlushCoalescer();
        if (related) Assert.Equal(IssueOutcome.QuestFail, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        else Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var pupil = Get<Hero>(instance, pupilId);
                Assert.Equal(!related, Get<Hero>(instance, giverId).Issue != null);
                Assert.Equal(related ? 0 : 1, Get<MobileParty>(instance, partyIds[0]).MemberRoster.GetTroopCount(pupil.CharacterObject));
                Assert.Equal(0, JewelryCount(Get<MobileParty>(instance, partyIds[0])));
                if (related) Assert.Null(pupil.CompanionOf);
                Assert.True(second.QuestOf(Server).IsOngoing);
            });
    }

    [Fact]
    public void RejoinRequestsAddOnlyTheOwnersPupilOnce()
    {
        Accept();
        var second = AcceptObserversQuest();
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).FirstConversationEndConsequence());
        Server.Call(() =>
        {
            var pupil = Get<Hero>(Server, pupilId);
            Get<MobileParty>(Server, partyIds[0]).MemberRoster.AddToCounts(pupil.CharacterObject, -1);
            using (new AllowedThread()) pupil.StayingInSettlement = Get<Settlement>(Server, settlementId);
        });
        environment.FlushCoalescer();
        Owner.Call(() =>
        {
            var quest = scenario.QuestOf(Owner);
            for (var i = 0; i < 2; i++)
                Owner.Resolve<IMessageBroker>().Publish(this, new QuestBranchRequested(quest, (byte)LordsNeedsTutorBranch.Rejoin));
        });
        environment.FlushCoalescer();
        Server.Call(() =>
        {
            var pupil = Get<Hero>(Server, pupilId);
            Assert.Equal(1, Get<MobileParty>(Server, partyIds[0]).MemberRoster.GetTroopCount(pupil.CharacterObject));
            Assert.Equal(0, Get<MobileParty>(Server, partyIds[1]).MemberRoster.GetTroopCount(pupil.CharacterObject));
            Assert.True(second.QuestOf(Server).IsOngoing);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CaptureEventRequestsEscapeOnlyForThePupilWhoseOwnerWasCaptured(bool ownerCaptured)
    {
        Accept();
        AcceptObserversQuest();
        using var escape = new MethodCallRecorder(AccessTools.Method(typeof(EndCaptivityAction), nameof(EndCaptivityAction.ApplyInternal)));
        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, partyIds[0]);
            using (new AllowedThread())
            {
                var map = new MapEvent();
                var side = new MapEventSide(map, BattleSideEnum.Defender, party.Party);
                side._battleParties.Add(new MapEventParty(party.Party));
                party.Party._mapEventSide = side;
                var pupil = Get<Hero>(Server, pupilId);
                Assert.True(pupil.IsPlayerCompanion);
                Assert.True(side.Parties.Single().Troops.FindIndexOfCharacter(pupil.CharacterObject).IsValid);
            }
            CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(Get<MobileParty>(Server, partyIds[1]).Party,
                Get<Hero>(Server, heroIds[ownerCaptured ? 0 : 1]));
        });
        Assert.Equal(ownerCaptured ? 1 : 0, escape.CountFor(Server));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }
}
