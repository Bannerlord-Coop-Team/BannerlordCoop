using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using LiteNetLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Exercises registered issue cancellation through the patched vanilla entry points.</summary>
public class IssueCancellationTests : IDisposable
{
    // Synthetic heroes have no encyclopedia entries for the journal text links
    private static readonly MethodBase[] JournalTextLinks =
    {
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
    };

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
        scenario.SendTroops(environment.CreateRegisteredObject<Hero>(), environment.CreateRegisteredObject<CharacterObject>());
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

    [Fact]
    public void PlayerCharacterChangeCancelThatThrowsOutOfTheManagerIsStillAnnounced() =>
        AssertCancelThatThrowsOutOfTheManagerIsStillAnnounced((owner, originalParty) =>
            scenario.Server.Resolve<IIssueCancellationCoordinator>().CancelForPlayerDeath(QuestScenario.ControllerA, owner, originalParty));

    [Fact]
    public void GiverDeathCancelThatThrowsOutOfTheManagerIsStillAnnounced() =>
        AssertCancelThatThrowsOutOfTheManagerIsStillAnnounced((_, _) =>
        {
            using (new AllowedThread()) Giver().ChangeState(Hero.CharacterStates.Dead);
            Campaign.Current.IssueManager.OnHeroKilled(Giver(), null, KillCharacterAction.KillCharacterActionDetail.None, false);
        });

    [Fact]
    public void GiverOwnerChangeCancelThatThrowsOutOfTheManagerIsStillAnnounced()
    {
        var heirId = environment.CreateRegisteredObject<Hero>();
        AssertCancelThatThrowsOutOfTheManagerIsStillAnnounced((_, _) =>
        {
            using (new AllowedThread()) Giver().ChangeState(Hero.CharacterStates.Dead);
            Campaign.Current.IssueManager.ChangeIssueOwner(Giver().Issue, scenario.Server.GetRegisteredObject<Hero>(heirId));
        });
    }

    [Fact]
    public void IssueCancelThatThrowsOutOfTheManagerIsStillAnnounced() =>
        AssertCancelThatThrowsOutOfTheManagerIsStillAnnounced((_, _) => Giver().Issue.CompleteIssueWithCancel());

    // The owner's accepted quest is cancelled on the server by the given entry, given the owner and its party
    private void AssertCancelThatThrowsOutOfTheManagerIsStillAnnounced(Action<Hero, MobileParty> cancel)
    {
        scenario.Accept(scenario.ClientA);
        var errors = new List<Exception>();
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var originalParty));
            using (new AllowedThread()) owner.PartyBelongedTo = originalParty;
            // IssueFinalized throws once DeactivateIssue took the issue out, so no quest outcome is observed
            using var throwing = new ThrowingMethodsScope(AccessTools.Method(typeof(IssueBase), nameof(IssueBase.RemoveAllTrackedObjects)));
            try
            {
                cancel(owner, originalParty);
            }
            catch (Exception e)
            {
                errors.Add(e);
            }
        }, JournalTextLinks);
        environment.FlushCoalescer();

        // Clients drop it and the controller's next character can't collect it
        AssertFinalizedEverywhere();
        scenario.Server.Call(() => Assert.False(scenario.Server.Resolve<IIssueOwnershipRegistry>()
            .TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _)));
        Assert.Empty(errors);
    }

    [Fact]
    public void PlayerCharacterChangeCancelThatThrowsBeforeTheJournalEndsTheRowAsACancel()
    {
        scenario.Accept(scenario.ClientA);
        IssueBase issue = null!;
        scenario.Server.Call(() =>
        {
            issue = Giver().Issue;
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var originalParty));
            new JournalLogsCampaignBehavior().RegisterEvents();
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
            {
                CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.PlayerStartedIssueQuestClassicSolution, owner);
                issue.IssueQuest.AddLog(new TaleWorlds.Localization.TextObject("Quest progress line"));
            }
            // Added last so it runs before the journal listener, and the issue stays in the manager
            CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, (_, _) => ThrowingMethodsScope.Throw());
            try
            {
                scenario.Server.Resolve<IIssueCancellationCoordinator>()
                    .CancelForPlayerDeath(QuestScenario.ControllerA, owner, originalParty);
            }
            finally
            {
                CampaignEvents.OnQuestCompletedEvent.ClearListeners(this);
            }
        }, JournalTextLinks);
        scenario.Server.Call(() => Campaign.Current.IssueManager.DailyTick());
        environment.FlushCoalescer();

        AssertFinalizedEverywhere();
        // The connected player's history ends as a cancel instead of being deleted, keeping its quest's own lines
        scenario.Server.Call(() =>
        {
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsRelatedTo(issue)));
            Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel, archive._lastIssueStatus);
            Assert.Single(archive.GetEntries(), entry => entry.LogText.ToString() == "Quest progress line");
        });
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOwnerQuestDismissalThatThrowsEndsOnceAndDropsTheOpenJournal(bool otherPlayerLeaves)
    {
        scenario.Accept(scenario.ClientA);
        IssueBase issue = null!;
        scenario.Server.Call(() => issue = Giver().Issue);

        KillOwnerlessGiverWhileFinalizingThrows(issue, IssueBase.IssueUpdateDetails.PlayerStartedIssueQuestClassicSolution, otherPlayerLeaves);

        AssertFinalizedEverywhere();
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>());
        AssertNoOpenJournal(issue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOwnerTroopDismissalThatThrowsEndsOnceWithTheCancelAndDropsTheOpenJournal(bool otherPlayerLeaves)
    {
        scenario.SendTroops(environment.CreateRegisteredObject<Hero>(), environment.CreateRegisteredObject<CharacterObject>());
        IssueBase issue = null!;
        scenario.Server.Call(() =>
        {
            issue = Giver().Issue;
            // Troops not due back yet, so only the retry ends the issue, with the cancel the coordinator asked for
            issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(5f);
            issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.DaysFromNow(4f);
            // Remembered at acceptance, so the troop ending still reaches the owner once the owner entry is gone
            scenario.Server.Resolve<IMessageBroker>().Publish(this,
                new AlternativeSolutionAcceptedOnServer(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
        });

        KillOwnerlessGiverWhileFinalizingThrows(issue, IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, otherPlayerLeaves);

        AssertFinalizedEverywhere();
        // The owner hears once that its troops are back
        Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel,
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>()).Ending);
        AssertNoOpenJournal(issue);
    }

    // The giver dies after the owner accepted and the owner entry is gone, and IssueFinalized throws in that ending
    private void KillOwnerlessGiverWhileFinalizingThrows(IssueBase issue, IssueBase.IssueUpdateDetails accepted, bool otherPlayerLeaves)
    {
        var errors = new List<Exception>();
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            new JournalLogsCampaignBehavior().RegisterEvents();
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                CampaignEventDispatcher.Instance.OnIssueUpdated(issue, accepted, owner);
            Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsRelatedTo(issue)));
            scenario.Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
            using (new AllowedThread()) Giver().ChangeState(Hero.CharacterStates.Dead);

            using var throwing = new ThrowingMethodsScope(AccessTools.Method(typeof(IssueBase), nameof(IssueBase.IssueFinalized)));
            try
            {
                Campaign.Current.IssueManager.OnHeroKilled(Giver(), null, KillCharacterAction.KillCharacterActionDetail.None, false);
            }
            catch (Exception e)
            {
                errors.Add(e);
            }
        }, JournalTextLinks);

        // A sweep in between finds the issue ownerless, but the daily retry already finishes it with the outcome it had
        if (otherPlayerLeaves)
        {
            scenario.Server.Call(() => scenario.Server.Resolve<IMessageBroker>().Publish(this,
                new PlayerDisconnected(scenario.ClientB.NetPeer, default(DisconnectInfo))), JournalTextLinks);
            scenario.Server.PumpGameThread();
        }

        scenario.Server.Call(() => Campaign.Current.IssueManager.DailyTick());
        environment.FlushCoalescer();
        Assert.Empty(errors);
    }

    // No owner is left to end the accepted row, so it goes instead of staying open in the server save
    private void AssertNoOpenJournal(IssueBase issue) =>
        scenario.Server.Call(() => Assert.DoesNotContain(
            Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsRelatedTo(issue)), entry => !entry.IsEnded()));

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
        scenario.SendTroops(companionId, troopId);
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
        scenario.SendTroops(companionId, troopId);
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
        scenario.SendTroops(companionId, environment.CreateRegisteredObject<CharacterObject>());
        scenario.Server.Call(() =>
        {
            var issue = Giver().Issue;
            scenario.Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
            Assert.False(scenario.Server.Resolve<IIssueCancellationCoordinator>().BeforeGiverDeath(Giver(), out var scope));
            Assert.Null(scope);
            Assert.False(scenario.Server.Resolve<IIssueCancellationCoordinator>().IsPending(issue));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.Null(Giver().Issue);
            issue.CompleteIssueWithCancel();
        });
        AssertFinalizedEverywhere();
    }

    [Fact]
    public void DetachedOngoingIssueDoesNotEnterCancellationRetryQueue()
    {
        scenario.Server.Call(() =>
        {
            var issue = Giver().Issue;
            var replacement = new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(
                Giver(), null);
            replacement.StringId = "replacement_issue";
            replacement._issueState = IssueBase.IssueState.Ongoing;
            Campaign.Current.IssueManager._issues[Giver()] = replacement;
            Giver().OnIssueCreatedForHero(replacement);

            Assert.False(scenario.Server.Resolve<IIssueCancellationCoordinator>()
                .BeforeCancellation(issue, new TaleWorlds.Localization.TextObject("cancel"), out var scope));
            Assert.Null(scope);
            Assert.False(scenario.Server.Resolve<IIssueCancellationCoordinator>().IsPending(issue));
        });
    }

    [Fact]
    public void DeadGiverCancelsInsteadOfCompletingTheAlternativeSolution()
    {
        scenario.SendTroops(environment.CreateRegisteredObject<Hero>(), environment.CreateRegisteredObject<CharacterObject>());
        scenario.Server.Call(() =>
        {
            using (new AllowedThread()) Giver()._heroState = Hero.CharacterStates.Dead;
            Giver().Issue.CompleteIssueWithAlternativeSolution();
        });
        AssertFinalizedEverywhere();
    }

    // Vanilla returned the troops before the cancel listener throws
    [Fact]
    public void DeadGiverCancelListenerThatThrowsReturnsTheTroopsOnceAndTellsTheOwnerOnce() =>
        AssertDeadGiverCancelThatThrowsReturnsTheTroopsOnce(() =>
            CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, (_, detail, _) =>
            {
                if (detail == IssueBase.IssueUpdateDetails.IssueCancel) ThrowingMethodsScope.Throw();
            }));

    // The troops returned line's log listener throws before vanilla returns them
    [Fact]
    public void DeadGiverCancelLogListenerThatThrowsReturnsTheTroopsOnceAndTellsTheOwnerOnce() =>
        AssertDeadGiverCancelThatThrowsReturnsTheTroopsOnce(() =>
            CampaignEvents.IssueLogAddedEvent.AddNonSerializedListener(this, (_, _) => ThrowingMethodsScope.Throw()));

    // The giver's death news throws before vanilla reaches the cancel
    [Fact]
    public void DeadGiverDeathNewsThatThrowsReturnsTheTroopsOnceAndTellsTheOwnerOnce() =>
        AssertDeadGiverCancelThatThrowsReturnsTheTroopsOnce(() =>
        {
            using var throwing = new ThrowingMethodsScope(AccessTools.Method(typeof(GameTexts), nameof(GameTexts.FindText)));
            Campaign.Current.IssueManager.OnHeroKilled(Giver(), null, KillCharacterAction.KillCharacterActionDetail.None, false);
        });

    // ControllerA's troops are out for a giver who just died, and the given fault breaks the first cancel on the server
    private void AssertDeadGiverCancelThatThrowsReturnsTheTroopsOnce(Action breakTheFirstCancel)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        scenario.SendTroops(companionId, troopId);
        scenario.Server.Call(() =>
        {
            using (new AllowedThread()) Giver()._heroState = Hero.CharacterStates.Dead;
            var issue = Giver().Issue;
            breakTheFirstCancel();
            // The next due tick must not cancel the issue and return the same troops again
            try
            {
                for (var day = 0; day < 2; day++) issue.CompleteIssueWithAlternativeSolution();
            }
            finally
            {
                CampaignEvents.IssueLogAddedEvent.ClearListeners(this);
                CampaignEvents.OnIssueUpdatedEvent.ClearListeners(this);
            }
            // Once it can be detached, the retry ends it
            Campaign.Current.IssueManager.DailyTick();
        });
        environment.FlushCoalescer();

        // A throw before vanilla returned them returns them once in the ending, the return never runs a second time
        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(3, party.MemberRoster.GetTroopCount(troop));
            });
        }
        AssertFinalizedEverywhere();
        // The owner's accepted journal hears the cancel once, also when it threw before vanilla's cancel event, and its
        // troops returned line is true
        var journal = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>());
        Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel, journal.Ending);
        Assert.Single(journal.Entries, entry => entry.Text.Value == "{=V5Za6d4h}Your troops have returned from their mission.");
    }

    [Fact]
    public void QuestCancelWhoseIssueCannotBeFinalizedLeavesTheManagerWithoutCancellingTheQuestAgain()
    {
        scenario.Accept(scenario.ClientA);
        var questEndings = 0;
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var originalParty));
            using (new AllowedThread()) owner.PartyBelongedTo = originalParty;
            CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, (_, _) => questEndings++);
            // The issue keeps its quest, so vanilla's DeactivateIssue would cancel that quest again instead of removing it
            try
            {
                using var throwing = new ThrowingMethodsScope(AccessTools.Method(typeof(IssueBase), nameof(IssueBase.IssueFinalized)));
                scenario.Server.Resolve<IIssueCancellationCoordinator>()
                    .CancelForPlayerDeath(QuestScenario.ControllerA, owner, originalParty);
                Campaign.Current.IssueManager.DailyTick();
            }
            finally
            {
                CampaignEvents.OnQuestCompletedEvent.ClearListeners(this);
            }
        }, JournalTextLinks);
        environment.FlushCoalescer();

        Assert.Equal(1, questEndings);
        AssertFinalizedEverywhere();
        scenario.Server.Call(() => Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(Giver())));
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
