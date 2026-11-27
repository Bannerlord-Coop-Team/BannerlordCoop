using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Messages;
using GameInterface.Services.UI.Notifications;
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
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// A removed player's issues end with it, never reaching its next character. A disconnect ends a quest the same way.
/// </summary>
public class RemovedPlayerIssueTests : IDisposable
{
    public enum Removal { Delete, GameOver, Retire }

    // The obituary is a game content lookup the harness can't serve, and the retirement record needs the full session
    private static readonly MethodBase[] DisabledMethods =
    {
        AccessTools.Method(typeof(KillCharacterAction), "CreateObituary"),
        AccessTools.Method(typeof(ApplyHeirSelectionActionInterface), "RecordRetirement"),
    };

    // Synthetic heroes have no encyclopedia entries for the journal text links
    private static readonly MethodBase[] JournalTextLinks =
    {
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
    };

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly List<IssueBase.IssueUpdateDetails> updates = new();
    // The scenario's issue on each machine, so a giver's next issue never passes for it
    private readonly Dictionary<EnvironmentInstance, IssueBase> issues = new();
    private Hero.CharacterStates? companionStateWhenKilled;

    private IEnumerable<EnvironmentInstance> Instances => environment.Clients.Prepend(environment.Server);

    public RemovedPlayerIssueTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        foreach (var instance in Instances)
            instance.Call(() => issues[instance] = instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue);
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(Removal.Delete)]
    [InlineData(Removal.Retire)]
    public void RemovingThePlayerEndsItsSentTroopsQuietlyAndFreesTheCompanion(Removal removal)
    {
        var companionId = SendTroopsWithACompanion();

        Remove(removal);

        AssertSentTroopsEndedQuietly(companionId, Hero.CharacterStates.Active);
        Assert.Null(companionStateWhenKilled);
    }

    [Fact]
    public void GameOverFreesTheSentCompanionBeforeTheCleanupKillsIt()
    {
        var companionId = SendTroopsWithACompanion();
        // A real companion is a wanderer of the owner's clan, so it is no heir and dies when the cleanup destroys the clan
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var owner = instance.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
                var companion = instance.GetRegisteredObject<Hero>(companionId);
                using (new AllowedThread())
                {
                    companion.Occupation = Occupation.Wanderer;
                    companion.CompanionOf = owner.Clan;
                }
            });
        }
        StageGameOver();

        Remove(Removal.GameOver);

        AssertSentTroopsEndedQuietly(companionId, Hero.CharacterStates.Dead);
        // Freed by the dismissal before the cleanup killed it, never killed while still on the mission
        Assert.Equal(Hero.CharacterStates.Active, companionStateWhenKilled);
    }

    // ControllerA's troops are out with a companion, whose state is recorded if anything kills it
    private string SendTroopsWithACompanion()
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        scenario.SendTroops(companionId, environment.CreateRegisteredObject<CharacterObject>());
        scenario.Server.Call(() =>
        {
            var companion = scenario.Server.GetRegisteredObject<Hero>(companionId);
            CampaignEvents.BeforeHeroKilledEvent.AddNonSerializedListener(this, (victim, _, _, _) =>
            {
                if (victim == companion) companionStateWhenKilled = victim.HeroState;
            });
        });
        return companionId;
    }

    private void AssertSentTroopsEndedQuietly(string companionId, Hero.CharacterStates companionState)
    {
        AssertEnded(IssueOutcome.AlternativeSolution);
        Assert.DoesNotContain(updates, detail => detail is IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest
            or IssueBase.IssueUpdateDetails.SentTroopsFailedQuest);
        // Server only, the bootstrap stub on Hero.ChangeState skips the patches that sync hero state to clients
        scenario.Server.Call(() =>
        {
            var companion = scenario.Server.GetRegisteredObject<Hero>(companionId);
            Assert.Null(companion.PartyBelongedTo);
            Assert.Equal(companionState, companion.HeroState);
        });
    }

    [Theory]
    [InlineData(Removal.Delete)]
    [InlineData(Removal.Retire)]
    public void RemovingThePlayerEndsSentTroopsWhoseAcceptThrewAfterTheyLeft(Removal removal)
    {
        SendTroopsThroughAnAcceptThatThrows();

        Remove(removal);

        AssertEnded(IssueOutcome.AlternativeSolution);
    }

    // A listener on vanilla's accept event throws once the troops left, and the accept's catch drops the owner entry
    private void SendTroopsThroughAnAcceptThatThrows()
    {
        const int sentTroops = 40;
        using var preconditions = new StubIssuePreconditions(QuestScenario.IssueType);
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    instance.GetRegisteredObject<CharacterObject>(troopId).Level = 16;
                    instance.GetRegisteredObject<Hero>(companionId)._partyBelongedTo = instance.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId);
                }
            });
        }
        scenario.Server.Call(() =>
        {
            var party = scenario.Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId);
            party.MemberRoster.AddToCounts(scenario.Server.GetRegisteredObject<Hero>(companionId).CharacterObject, 1);
            party.MemberRoster.AddToCounts(scenario.Server.GetRegisteredObject<CharacterObject>(troopId), sentTroops);
            CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, (_, detail, _) =>
            {
                if (detail == IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest) ThrowingMethodsScope.Throw();
            });
        });
        environment.FlushCoalescer();

        var client = scenario.ClientA;
        client.Call(() =>
        {
            var owner = client.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            var party = client.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId);
            using (new MainHeroSubstitutionScope(owner, party))
            {
                var issue = client.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
                using (new AllowedThread())
                    issue.AlternativeSolutionSentTroops.AddToCounts(client.GetRegisteredObject<Hero>(companionId).CharacterObject, 1);
                AlternativeSolutionPickerDriver.Commit(client, issue, owner, party, client.GetRegisteredObject<CharacterObject>(troopId), sentTroops);
                issue.StartIssueWithAlternativeSolution();
            }
            // The server's accept runs inside this call, and its start log reads a settlement the test giver lacks
        }, JournalTextLinks.Append(AccessTools.PropertyGetter(typeof(AllowedIssue), "AlternativeSolutionStartLog")));
        environment.FlushCoalescer();

        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAcceptRejected>());
        scenario.Server.Call(() =>
        {
            Assert.True(issues[scenario.Server].IsSolvingWithAlternative);
            Assert.False(scenario.Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
        });
    }

    [Fact]
    public void CompanionWhoseDeathAlreadyRanStaysDisabledWhenThePlayerIsDeleted()
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        scenario.SendTroops(companionId, environment.CreateRegisteredObject<CharacterObject>());
        scenario.Server.Call(() =>
        {
            // Freeing it would activate a hero vanilla's OnHeroActivated can't handle without a developer
            using (new AllowedThread()) scenario.Server.GetRegisteredObject<Hero>(companionId).OnDeath();
        });

        Remove(Removal.Delete);

        AssertEnded(IssueOutcome.AlternativeSolution);
        scenario.Server.Call(() =>
            Assert.Equal(Hero.CharacterStates.Disabled, scenario.Server.GetRegisteredObject<Hero>(companionId).HeroState));
    }

    [Fact]
    public void EndingThatThrowsAfterLeavingTheManagerIsAnnouncedAtOnceAndKeepsTheNextIssuesOwner()
    {
        var archive = SendTroopsDueBackWithAnOpenRow();

        // IssueFinalized throws once DeactivateIssue took the issue out of the manager
        Remove(Removal.Delete, AccessTools.Method(typeof(IssueBase), nameof(IssueBase.RemoveAllTrackedObjects)));

        // Clients drop it at once, so the giver's next issue a player accepts reaches every machine
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        var nextId = CreateNextIssueEverywhere();
        scenario.Server.Call(() => Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>()
            .TrySetOwner(scenario.NotableId, nextId, QuestScenario.ControllerB)));
        var payouts = DailyTickCountingPayouts();

        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(nextId, instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue?.StringId));
        scenario.Server.Call(() => Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>()
            .TryGetOwner(scenario.NotableId, nextId, out _)));
        AssertEndedOnceWithoutPaying(archive, payouts);
    }

    [Fact]
    public void EndingThatThrowsInTheManagerIsAnnouncedByTheDailyRetryWithoutPaying()
    {
        var archive = SendTroopsDueBackWithAnOpenRow();

        // IssueFinalized throws before it ran, so the issue stays in the manager
        Remove(Removal.Delete, AccessTools.Method(typeof(IssueBase), nameof(IssueBase.IssueFinalized)));

        // The owner entry no longer names the controller its next character comes back on, and no client hears of it yet
        scenario.Server.Call(() =>
        {
            Assert.True(InManager(scenario.Server));
            Assert.False(scenario.Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        var payouts = DailyTickCountingPayouts();

        foreach (var instance in Instances)
            instance.Call(() => Assert.False(InManager(instance)));
        AssertEndedOnceWithoutPaying(archive, payouts);
    }

    // ControllerA's troops are due back with a sure success, so a tick that let vanilla run would pay, and its row is open
    private JournalLogEntry SendTroopsDueBackWithAnOpenRow()
    {
        scenario.SendTroops(environment.CreateRegisteredObject<Hero>(), environment.CreateRegisteredObject<CharacterObject>());
        JournalLogEntry archive = null!;
        scenario.Server.Call(() =>
        {
            var issue = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            issue._failureChance = 0f;
            issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(-1f);
            var owner = scenario.Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            new JournalLogsCampaignBehavior().RegisterEvents();
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, owner);
            archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsRelatedTo(issue)));
        }, JournalTextLinks);
        return archive;
    }

    // The payout reads the giver's town these test heroes lack, so a wrongly run one is counted and skipped, not thrown
    private int DailyTickCountingPayouts()
    {
        var payouts = 0;
        scenario.Server.Call(() =>
        {
            using var recorder = new MethodCallRecorder(
                AccessTools.Method(typeof(AllowedIssue), "AlternativeSolutionEndWithSuccessConsequence"));
            Campaign.Current.IssueManager.DailyTick();
            payouts = recorder.Count;
        });
        environment.FlushCoalescer();
        return payouts;
    }

    private void AssertEndedOnceWithoutPaying(JournalLogEntry archive, int payouts)
    {
        foreach (var instance in Instances)
            instance.Call(() => Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _)));
        Assert.Equal(IssueOutcome.AlternativeSolution,
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Equal(0, payouts);
        Assert.DoesNotContain(updates, detail => detail is IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest
            or IssueBase.IssueUpdateDetails.SentTroopsFailedQuest);
        // No owner hero is left to end the owner's row, so it goes instead of staying open in the server save
        scenario.Server.Call(() => Assert.DoesNotContain(archive, Campaign.Current.LogEntryHistory.GameActionLogs));
    }

    // The giver's next issue built by vanilla on the server and sent to the clients, vanilla's AfterIssueCreation reads the giver's town
    private string CreateNextIssueEverywhere()
    {
        var townId = environment.CreateRegisteredObject<Settlement>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                    instance.GetRegisteredObject<Hero>(scenario.NotableId).StayingInSettlement = instance.GetRegisteredObject<Settlement>(townId);
            });
        }

        string nextId = null!;
        scenario.Server.Call(() =>
        {
            var notable = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId);
            var potentialIssue = new PotentialIssueData((in PotentialIssueData _, Hero hero) => new AllowedIssue(hero, null),
                typeof(AllowedIssue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, notable));
            nextId = notable.Issue.StringId;
        });
        environment.FlushCoalescer();
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        // Taken, so the daily tick skips the stay alive check that reads the town's security
        foreach (var instance in Instances)
            instance.Call(() => instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution);
        return nextId;
    }

    [Fact]
    public void GameOverEndsTheRemovedPlayersQuestAndDropsItsUnfinishedJournal()
    {
        // The game over player stays connected, so no disconnect sweep would ever end it
        TakeQuestAsPlayerA();
        var archive = AcceptQuestOnServer();
        StageGameOver();

        Remove(Removal.GameOver);

        AssertEnded(IssueOutcome.QuestCancel);
        // Its registration is gone, so no owner hero can end the row and the server save would keep it open
        scenario.Server.Call(() => Assert.DoesNotContain(archive, Campaign.Current.LogEntryHistory.GameActionLogs));
    }

    [Fact]
    public void DisconnectKeepsTheSuccessVanillaAlreadyWrote()
    {
        TakeQuestAsPlayerA();
        var archive = AcceptQuestOnServer();
        scenario.Server.Call(() =>
        {
            var issue = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            var owner = scenario.Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess, owner);
            // Vanilla wrote the success, then IssueFinalized threw, so the issue is still registered with its owner
            using (new AllowedThread()) issue.IssueQuest._questState = QuestBase.QuestStates.Finalized;
            Assert.True(archive.IsEnded());
        }, JournalTextLinks);

        Disconnect(scenario.ClientA);

        AssertEnded(IssueOutcome.QuestCancel);
        scenario.Server.Call(() =>
        {
            Assert.Same(archive, Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId))));
            Assert.Equal(IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess, archive._lastIssueStatus);
        });
    }

    [Fact]
    public void DisconnectDropsTheOpenRowOfAnIssueWhoseOwnerIsGone()
    {
        TakeQuestAsPlayerA();
        var archive = AcceptQuestOnServer();
        JournalLogEntry unrelated = null!;
        scenario.Server.Call(() =>
        {
            unrelated = new JournalLogEntry(new TextObject("Unrelated active quest"), null, null, false);
            Campaign.Current.LogEntryHistory._logs.Add(unrelated);
            // Nothing resolves its owner hero any more, so nobody is left to end the row
            scenario.Server.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
        });

        Disconnect(scenario.ClientB);

        // The open row goes instead of staying in the server save, other rows stay
        scenario.Server.Call(() =>
        {
            Assert.DoesNotContain(archive, Campaign.Current.LogEntryHistory.GameActionLogs);
            Assert.Contains(unrelated, Campaign.Current.LogEntryHistory.GameActionLogs);
            Assert.False(InManager(scenario.Server));
        });
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    private void TakeQuestAsPlayerA()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
            });
        }
    }

    private void Disconnect(EnvironmentInstance leaver)
    {
        scenario.Server.Call(() =>
        {
            scenario.Server.Resolve<IPlayerManager>().ClearPeer(leaver.NetPeer);
            scenario.Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(leaver.NetPeer, default(DisconnectInfo)));
        }, JournalTextLinks);
        environment.FlushCoalescer();
    }

    [Fact]
    public void RemovingAPlayerLeavesAnotherPlayersSentTroopsAlone()
    {
        const string otherIssueId = "issue_other";
        var otherNotableId = environment.CreateRegisteredObject<Hero>();
        scenario.AddIssue(otherNotableId, otherIssueId);
        scenario.SendTroops(environment.CreateRegisteredObject<Hero>(), environment.CreateRegisteredObject<CharacterObject>());
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                instance.GetRegisteredObject<Hero>(otherNotableId).Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(otherNotableId, otherIssueId, QuestScenario.ControllerB));
            });
        }
        // Offline but still registered, so only the removed controller tells the two apart
        scenario.Server.Call(() => scenario.Server.Resolve<IPlayerManager>().ClearPeer(scenario.ClientB.NetPeer));

        Remove(Removal.Delete);

        AssertEnded(IssueOutcome.AlternativeSolution);
        scenario.Server.Call(() =>
        {
            Assert.NotNull(scenario.Server.GetRegisteredObject<Hero>(otherNotableId).Issue);
            Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(otherNotableId, otherIssueId, out var owner));
            Assert.Equal(QuestScenario.ControllerB, owner);
        });
    }

    // The quest and its owned journal row the server writes when ControllerA accepts it
    private JournalLogEntry AcceptQuestOnServer()
    {
        JournalLogEntry archive = null!;
        scenario.Server.Call(() =>
        {
            var issue = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            var owner = scenario.Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            var journals = new JournalLogsCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { journals }));
            journals.RegisterEvents();
            using (new AllowedThread())
                issue.IssueQuest = new UnstartedQuest("quest_removed_player", issue.IssueOwner);
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.PlayerStartedIssueQuestClassicSolution, owner);
            archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsRelatedTo(issue)));
            Assert.Equal(QuestScenario.ControllerA, Assert.IsType<PlayerJournalLogEntry>(archive).OwnerControllerId);
            Assert.False(archive.IsEnded());
        }, JournalTextLinks);
        return archive;
    }

    private void StageGameOver()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var owner = instance.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
                using (new AllowedThread()) owner.Clan.SetLeader(owner);
            });
        }
        scenario.Server.Call(() => scenario.Server.GetRegisteredObject<Hero>(scenario.OwnerHeroId)
            .ChangeState(Hero.CharacterStates.Dead));
    }

    private void Remove(Removal removal, params MethodBase[] failing)
    {
        scenario.Server.Call(() =>
        {
            using var throwing = new ThrowingMethodsScope(failing);
            CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, (_, detail, _) => updates.Add(detail));
            var broker = scenario.Server.Resolve<IMessageBroker>();
            if (removal == Removal.Retire)
                broker.Publish(scenario.ClientA.NetPeer, new NetworkRequestPlayerRetirement());
            else
                broker.Publish(scenario.ClientA.NetPeer,
                    new NetworkRequestDeletePlayer(scenario.OwnerHeroId, keepConnected: removal == Removal.GameOver));
        }, DisabledMethods);
        environment.FlushCoalescer();
    }

    // Reads the manager of the instance whose call runs it, so only call it inside that instance's call
    private bool InManager(EnvironmentInstance instance) =>
        Campaign.Current.IssueManager.Issues.Values.Any(issue => ReferenceEquals(issue, issues[instance]));

    private void AssertEnded(IssueOutcome outcome)
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.False(InManager(instance));
                Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
            });
        }
        Assert.Equal(outcome, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }
}
