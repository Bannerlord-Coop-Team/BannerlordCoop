using Common.Messaging;
using Common.Util;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Framework.Finalization;
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

    private void KillPupil()
    {
        var method = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
        var shim = Assert.Single(Harmony.GetPatchInfo(method).Prefixes, patch => patch.owner == "Coop.Testing");
        var bootstrap = new Harmony(shim.owner);
        bootstrap.Unpatch(method, shim.PatchMethod);
        try
        {
            Server.Call(() => KillCharacterAction.ApplyByMurder(Get<Hero>(Server, pupilId), showNotification: false));
        }
        finally
        {
            bootstrap.Patch(method, prefix: new HarmonyMethod(shim.PatchMethod));
        }
        Server.Call(() => Assert.False(Get<Hero>(Server, pupilId).IsAlive));
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
                Assert.Equal(0, Get<MobileParty>(instance, partyIds[0]).MemberRoster.GetTroopCount(pupil.CharacterObject));
                foreach (var skill in Skills.All) Assert.Equal(originalFocus[skill.StringId], pupil.HeroDeveloper.GetFocus(skill));
                Assert.NotNull(Get<Hero>(instance, second.NotableId).Issue);
                if (instance != Owner) Assert.True(Get<Hero>(instance, second.NotableId).Issue.IssueQuest.IsOngoing);
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
        using var escape = new MethodCallRecorder(AccessTools.Method(typeof(EndCaptivityAction), nameof(EndCaptivityAction.ApplyByEscape)));
        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, partyIds[0]);
            using (new AllowedThread())
            {
                var map = new MapEvent();
                var side = new MapEventSide(map, BattleSideEnum.Defender, party.Party);
                side._battleParties.Add(new MapEventParty(party.Party));
                party.Party._mapEventSide = side;
            }
            CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(Get<MobileParty>(Server, partyIds[1]).Party,
                Get<Hero>(Server, heroIds[ownerCaptured ? 0 : 1]));
        });
        Assert.Equal(ownerCaptured ? 1 : 0, escape.CountFor(Server));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }
}
