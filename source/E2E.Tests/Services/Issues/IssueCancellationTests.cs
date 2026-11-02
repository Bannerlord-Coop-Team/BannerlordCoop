using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Exercises registered issue cancellation through the patched vanilla entry points.</summary>
public class IssueCancellationTests : IDisposable
{
    private readonly StubIssueQuestGenerator quests = new();
    private readonly StubIssuePreconditions preconditions = new(QuestScenario.IssueType);
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;

    public IssueCancellationTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
    }

    public void Dispose()
    {
        environment.Dispose();
        preconditions.Dispose();
        quests.Dispose();
    }

    [Fact]
    public void CancelWithoutAQuestIsAnnouncedOnceAndClearedEverywhere()
    {
        scenario.Server.Call(() => Giver().Issue.CompleteIssueWithCancel());
        AssertFinalizedEverywhere();
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public void PlayerCharacterChangeCancelsOnlyItsPersonalMirrorBeforeInheritance(bool dead, bool special, bool hasOriginalParty)
    {
        scenario.Accept(scenario.ClientA);
        var otherGiverId = environment.CreateRegisteredObject<Hero>();
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var originalParty));
            if (!hasOriginalParty) originalParty = null;
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(otherGiverId, out var otherGiver));
            using (new AllowedThread())
            {
                owner._heroState = dead ? Hero.CharacterStates.Dead : Hero.CharacterStates.Active;
                owner._partyBelongedTo = null;
                var otherIssue = new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(otherGiver, null);
                otherIssue.StringId = "bystander_issue";
                otherIssue._issueState = IssueBase.IssueState.Ongoing;
                Campaign.Current.IssueManager._issues.Add(otherGiver, otherIssue);
                otherGiver.OnIssueCreatedForHero(otherIssue);
                otherGiver.Issue.IssueQuest = new UnstartedQuest("bystander_mirror", otherGiver);
                if (special) Giver().Issue.IssueQuest = new SpecialQuest("special_mirror", Giver());
            }
            Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(otherGiverId,
                otherGiver.Issue.StringId, QuestScenario.ControllerB));
            var quest = Giver().Issue.IssueQuest;
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            var calls = new List<(QuestBase, Hero, MobileParty, bool)>();
            CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, (completed, detail) =>
                calls.Add((completed, Hero.MainHero, MobileParty.MainParty, AllowedThread.IsThisThreadAllowed())));
            var previousHero = Hero.MainHero;
            var previousParty = MobileParty.MainParty;
            var cancellations = scenario.Server.Resolve<IIssueCancellationCoordinator>();
            cancellations.CancelForPlayerDeath(QuestScenario.ControllerA, owner, originalParty);
            cancellations.CancelForPlayerDeath(QuestScenario.ControllerA, owner, originalParty);
            if (!special)
            {
                var call = Assert.Single(calls);
                Assert.Same(quest, call.Item1);
                Assert.Same(owner, call.Item2);
                Assert.Same(originalParty ?? previousParty, call.Item3);
                Assert.False(call.Item4);
                Assert.True(quest.IsFinalized);
                Assert.Null(Giver().Issue);
            }
            else
            {
                Assert.Empty(calls);
                Assert.True(quest.IsOngoing);
            }
            Assert.True(otherGiver.Issue.IssueQuest.IsOngoing);
            Assert.Same(previousHero, Hero.MainHero);
            Assert.Same(previousParty, MobileParty.MainParty);
        });
        if (!special) AssertFinalizedEverywhere();
        else Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void PlayerDeathDoesNotReturnOrCancelPendingAlternativeTroops()
    {
        StageAlternative(environment.CreateRegisteredObject<Hero>(), environment.CreateRegisteredObject<CharacterObject>());
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var originalParty));
            owner.ChangeState(Hero.CharacterStates.Dead);
            owner._partyBelongedTo = null;
            var issue = Giver().Issue;
            scenario.Server.Resolve<IIssueCancellationCoordinator>()
                .CancelForPlayerDeath(QuestScenario.ControllerA, owner, originalParty);
            Assert.Same(issue, Giver().Issue);
            Assert.Equal(4, issue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.Empty(Campaign.Current.IssueManager._awaitingAlternativeSolutionTroops.GetTroopRoster());
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    private sealed class SpecialQuest : UnstartedQuest
    {
        public SpecialQuest(string questId, Hero giver) : base(questId, giver) { }
        public override string SpecialQuestType => "succession-test";
    }

    [Fact]
    public void CancelRunningIssueAnnouncesOnceAcrossNestedQuestCompletion()
    {
        scenario.Accept(scenario.ClientA);
        var ownerQuest = scenario.QuestOf(scenario.ClientA);
        scenario.ClientA.Call(() =>
        {
            using (new AllowedThread()) ownerQuest.StartQuest();
        });

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            using (new AllowedThread()) owner.PartyBelongedTo = party;
            var issue = Giver().Issue;
            var updates = new List<IssueBase>();
            CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, (updated, detail, solver) => updates.Add(updated));
            Giver().Issue.CompleteIssueWithCancel();
            Assert.Same(issue, Assert.Single(updates));
        });

        AssertFinalizedEverywhere();
        scenario.ClientA.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.DoesNotContain(ownerQuest, Campaign.Current.QuestManager.Quests);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientWorldCancellationWaitsForServerEvenDuringReplay(bool allowedThread)
    {
        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            if (allowedThread)
            {
                using (new AllowedThread()) giver.Issue.CompleteIssueWithCancel();
            }
            else giver.Issue.CompleteIssueWithCancel();
            Assert.NotNull(giver.Issue);
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GiverDeathWaitsForOwnerPartyAndRetriesOnce(bool alternative)
    {
        if (!alternative) scenario.Accept(scenario.ClientA);
        scenario.Server.Call(() =>
        {
            var issue = Giver().Issue;
            if (alternative)
            {
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
            }
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            using (new AllowedThread())
            {
                owner.PartyBelongedTo = null;
                Giver().ChangeState(Hero.CharacterStates.Dead);
            }
            Campaign.Current.IssueManager.OnHeroKilled(Giver(), null, KillCharacterAction.KillCharacterActionDetail.None, false);
            Assert.Same(issue, Giver().Issue);
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            using (new AllowedThread()) owner.PartyBelongedTo = party;
            CampaignEventDispatcher.Instance.HourlyTick();
            CampaignEventDispatcher.Instance.HourlyTick();
        });
        AssertFinalizedEverywhere();
    }

    [Fact]
    public void MissingOwnerDismissesAcceptedIssueInsteadOfLeavingARetryForever()
    {
        scenario.Accept(scenario.ClientA);
        scenario.Server.Call(() =>
        {
            scenario.Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
            using (new AllowedThread()) Giver().ChangeState(Hero.CharacterStates.Dead);
            Campaign.Current.IssueManager.OnHeroKilled(Giver(), null, KillCharacterAction.KillCharacterActionDetail.None, false);
        });
        AssertFinalizedEverywhere();
    }

    [Fact]
    public void GiverDeathServiceSuppliesAndRestoresOwnerScopeWithoutHarmony()
    {
        scenario.Accept(scenario.ClientA);
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            using (new AllowedThread()) owner.PartyBelongedTo = party;
            var previousHero = Hero.MainHero;
            var previousParty = MobileParty.MainParty;

            Assert.True(scenario.Server.Resolve<IIssueCancellationCoordinator>().BeforeGiverDeath(Giver(), out var scope));
            using (scope)
            {
                Assert.Same(owner, Hero.MainHero);
                Assert.Same(party, MobileParty.MainParty);
            }
            Assert.Same(previousHero, Hero.MainHero);
            Assert.Same(previousParty, MobileParty.MainParty);
        });
    }

    [Fact]
    public void CaptiveOwnerKeepsTroopsUntilTheyCanReturnAndTheyReturnOnlyOnce()
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        StageAlternative(companionId, troopId);
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            using (new AllowedThread()) owner._heroState = Hero.CharacterStates.Prisoner;
            Giver().Issue.CompleteIssueWithCancel();
            Assert.NotNull(Giver().Issue);
            Assert.Equal(4, Giver().Issue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.Equal(0, Campaign.Current.IssueManager._awaitingAlternativeSolutionTroops.TotalManCount);

            using (new AllowedThread()) owner._heroState = Hero.CharacterStates.Active;
            scenario.Server.Resolve<IIssueCancellationCoordinator>().Retry();
            scenario.Server.Resolve<IIssueCancellationCoordinator>().Retry();
        });

        environment.FlushCoalescer();
        AssertFinalizedEverywhere();
        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OtherPartyId, out var other));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(3, party.MemberRoster.GetTroopCount(troop));
                Assert.Equal(0, other.MemberRoster.GetTroopCount(troop));
                // The test bootstrap replaces ChangeState before its production field-store replication runs.
                if (instance == scenario.Server) Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlternativeCancellationUpdatesTheOwnerBeforeDetachingWithoutReturningTroopsAgain(bool triedBefore)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        StageAlternative(companionId, troopId);
        var updates = new List<(IssueBase Issue, IssueBase.IssueUpdateDetails Detail, Hero Solver, bool Attached, int Troops)>();
        Hero expectedSolver = null!;
        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            giver.Issue.IsTriedToSolveBefore = triedBefore;
            expectedSolver = triedBefore ? Hero.MainHero : null;
            CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, (issue, detail, solver) =>
                updates.Add((issue, detail, solver, giver.Issue == issue, issue.AlternativeSolutionSentTroops.TotalManCount)));
        });

        scenario.Server.Call(() => Giver().Issue.CompleteIssueWithCancel());
        environment.FlushCoalescer();

        var update = Assert.Single(updates);
        Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel, update.Detail);
        Assert.Equal(QuestScenario.IssueId, update.Issue.StringId);
        Assert.Same(expectedSolver, update.Solver);
        Assert.True(update.Attached);
        Assert.Equal(4, update.Troops);
        AssertFinalizedEverywhere();
        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OtherPartyId, out var other));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(3, party.MemberRoster.GetTroopCount(troop));
                Assert.Equal(0, other.MemberRoster.GetTroopCount(troop));
                Assert.Equal(0, Campaign.Current.IssueManager._awaitingAlternativeSolutionTroops.TotalManCount);
            });
        }
    }

    [Fact]
    public void MissingAlternativeOwnerFreesDisabledCompanionAndDismissesOnce()
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        StageAlternative(companionId, environment.CreateRegisteredObject<CharacterObject>());
        scenario.Server.Call(() =>
        {
            var issue = Giver().Issue;
            scenario.Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
            Assert.False(scenario.Server.Resolve<IIssueCancellationCoordinator>().BeforeGiverDeath(Giver(), out var scope));
            Assert.Null(scope);
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.Null(Giver().Issue);
            issue.CompleteIssueWithCancel();
        });
        AssertFinalizedEverywhere();
    }

    [Fact]
    public void DeadGiverCancelsInsteadOfCompletingTheAlternativeSolution()
    {
        StageAlternative(environment.CreateRegisteredObject<Hero>(), environment.CreateRegisteredObject<CharacterObject>());
        scenario.Server.Call(() =>
        {
            using (new AllowedThread()) Giver()._heroState = Hero.CharacterStates.Dead;
            Giver().Issue.CompleteIssueWithAlternativeSolution();
        });
        AssertFinalizedEverywhere();
    }

    private void StageAlternative(string companionId, string troopId)
    {
        var rosterId = environment.CreateRegisteredObject<TroopRoster>();
        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<TroopRoster>(rosterId, out var roster));
                using (new AllowedThread())
                {
                    party.Party.MemberRoster = roster;
                    roster.OwnerParty = party.Party;
                    owner.PartyBelongedTo = party;
                    giver.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 3);
                    companion._heroState = Hero.CharacterStates.Disabled;
                }
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
            });
        }
    }

    private Hero Giver()
    {
        Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
        return giver;
    }

    private void AssertFinalizedEverywhere()
    {
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.Null(giver.Issue);
            });
        }
    }
}
