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
