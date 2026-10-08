using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Alleys;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Visibility;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

public class AcceptVisibilityTests : IDisposable
{
    private const string IssueId = "issue_visibility";
    private const string OwnerControllerId = "player-A";
    private const string OtherControllerId = "player-B";
    private const int SentTroops = 10;

    private readonly List<(IssueBase.IssueUpdateDetails Details, Hero? Solver)> otherUpdates = new();
    private readonly List<QuestBase.QuestCompleteDetails> otherQuestCompletions = new();

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Owner => TestEnvironment.Clients.First();
    private EnvironmentInstance Other => TestEnvironment.Clients.Last();

    public AcceptVisibilityTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        Owner.Resolve<IControllerIdProvider>().SetControllerId(OwnerControllerId);
        Other.Resolve<IControllerIdProvider>().SetControllerId(OtherControllerId);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private static IssueBase AddIssue(EnvironmentInstance instance, string notableId)
    {
        IssueBase issue = null!;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                issue = new AllowedIssue(notable, null);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });

        return issue;
    }

    private void ListenForIssueUpdatesOnTheOtherClient()
    {
        Other.Call(() =>
        {
            CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(
                this,
                (IssueBase issue, IssueBase.IssueUpdateDetails details, Hero solver) => otherUpdates.Add((details, solver)));
        });
    }

    private void ListenForQuestCompletionsOnTheOtherClient()
    {
        Other.Call(() =>
        {
            CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(
                this,
                (QuestBase quest, QuestBase.QuestCompleteDetails details) => otherQuestCompletions.Add(details));
        });
    }

    private void LoadQuestSolutionFromSave(IssueBase issue, string notableId)
    {
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                issue.IssueQuest = new UnstartedQuest("quest_unstarted", notable);
                issue.IsTriedToSolveBefore = true;
            }
        });
    }

    private NetworkAlternativeSolutionAccepted TroopsAccepted(string notableId, string companionId, string troopId)
    {
        NetworkAlternativeSolutionAccepted accepted = default;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            var roster = TroopRoster.CreateDummyTroopRoster();

            using (new AllowedThread())
            {
                roster.AddToCounts(companion.CharacterObject, 1);
                roster.AddToCounts(troop, SentTroops);
            }

            var state = new AlternativeSolutionVanillaState(
                1f,
                0f,
                0,
                string.Empty,
                1000f,
                CampaignTime.DaysFromNow(10f).NumTicks,
                CampaignTime.DaysFromNow(9f).NumTicks,
                CampaignTime.DaysFromNow(10f).NumTicks);

            accepted = new NetworkAlternativeSolutionAccepted(
                notableId,
                IssueId,
                Server.Resolve<IAlleyGarrisonData>().ToData(roster),
                state,
                Array.Empty<byte>(),
                OwnerControllerId);
        });

        return accepted;
    }

    [Fact]
    public void SendTroops_OnlyTheSenderGetsTheTroopsAndTheLogEntry()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var ownerIssue = AddIssue(Owner, notableId);
        var otherIssue = AddIssue(Other, notableId);
        ListenForIssueUpdatesOnTheOtherClient();
        var accepted = TroopsAccepted(notableId, companionId, troopId);

        Owner.SimulateMessage(this, accepted);
        Other.SimulateMessage(this, accepted);

        Owner.Call(() =>
        {
            Assert.True(ownerIssue.IsSolvingWithAlternative);
            Assert.True(ownerIssue.IsTriedToSolveBefore);
            Assert.True(ownerIssue.AlternativeSolutionSentTroops.TotalManCount > 0);
        });

        Other.Call(() =>
        {
            Assert.True(otherIssue.IsSolvingWithAlternative);
            Assert.False(otherIssue.IsTriedToSolveBefore);
            Assert.Equal(0, otherIssue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.Empty(otherIssue.JournalEntries);
            Assert.Equal(accepted.State.ReturnTimeTicks, otherIssue.AlternativeSolutionReturnTimeForTroops.NumTicks);
        });

        Assert.Empty(otherUpdates);
    }

    [Fact]
    public void SendTroops_TheQuestScreenListsTheMissionOnlyForTheSender()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var ownerIssue = AddIssue(Owner, notableId);
        var otherIssue = AddIssue(Other, notableId);
        var accepted = TroopsAccepted(notableId, companionId, troopId);

        Owner.SimulateMessage(this, accepted);
        Other.SimulateMessage(this, accepted);

        Owner.Call(() => Assert.False(QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(ownerIssue)));
        Other.Call(() => Assert.True(QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(otherIssue)));
    }

    private static void LoadTroopsMissionFromSave(EnvironmentInstance instance, IssueBase issue, string troopId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            using (new AllowedThread())
            {
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue.AlternativeSolutionSentTroops.AddToCounts(troop, SentTroops);
            }
        });
    }

    private void ServerKnowsTheOwner(string notableId)
    {
        Server.Call(() => Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(notableId, IssueId, OwnerControllerId)));
    }

    [Fact]
    public void SendTroops_AClientThatJoinsLaterDoesNotCountTheMissionAsItsOwn()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var otherIssue = AddIssue(Other, notableId);
        ServerKnowsTheOwner(notableId);
        LoadTroopsMissionFromSave(Other, otherIssue, troopId);

        Server.SimulateMessage(this, new PlayerCampaignEntered(Other.NetPeer));

        Other.Call(() =>
        {
            Assert.True(otherIssue.AlternativeSolutionSentTroops.TotalManCount > 0);
            Assert.True(Other.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out var ownerControllerId));
            Assert.Equal(OwnerControllerId, ownerControllerId);
            Assert.True(QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(otherIssue));
        });
    }

    [Fact]
    public void SendTroops_AnOwnerWhoRejoinsGetsItsMissionBackInTheQuestScreen()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var ownerIssue = AddIssue(Owner, notableId);
        ServerKnowsTheOwner(notableId);
        LoadTroopsMissionFromSave(Owner, ownerIssue, troopId);

        Owner.Call(() => Assert.True(QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(ownerIssue)));

        Server.SimulateMessage(this, new PlayerCampaignEntered(Owner.NetPeer));

        Owner.Call(() => Assert.False(QuestScreenIssueFilterPatch.IsAnotherPlayersTroopsMission(ownerIssue)));
    }

    [Fact]
    public void SendTroops_AClientForgetsTheOwnerWhenTheIssueEnds()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        AddIssue(Other, notableId);
        var accepted = TroopsAccepted(notableId, companionId, troopId);

        Other.SimulateMessage(this, accepted);

        Other.Call(() =>
        {
            Assert.True(Other.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out var ownerControllerId));
            Assert.Equal(OwnerControllerId, ownerControllerId);
        });

        Other.SimulateMessage(this, new NetworkIssueFinalized(notableId, IssueId, IssueOutcome.AlternativeSolution));

        Other.Call(() => Assert.False(Other.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out _)));
    }

    [Theory]
    [InlineData(IssueOutcome.QuestSuccess)]
    [InlineData(IssueOutcome.QuestCancel)]
    public void QuestSolution_AClientThatJoinedLaterEndsTheIssueWithoutDrivingTheQuestFromTheSave(IssueOutcome outcome)
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var otherIssue = AddIssue(Other, notableId);
        ListenForIssueUpdatesOnTheOtherClient();
        ListenForQuestCompletionsOnTheOtherClient();
        LoadQuestSolutionFromSave(otherIssue, notableId);

        Other.SimulateMessage(this, new NetworkIssueFinalized(notableId, IssueId, outcome));

        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.Null(notable.Issue);
            Assert.Null(otherIssue.IssueQuest);
            Assert.False(otherIssue.IsTriedToSolveBefore);
        });

        Assert.Empty(otherQuestCompletions);
        var update = Assert.Single(otherUpdates);
        Assert.Null(update.Solver);
    }

    [Fact]
    public void QuestSolution_EveryClientRemembersWhoAccepted()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddIssue(Other, notableId);

        Other.SimulateMessage(this, new NetworkQuestSolutionAccepted(notableId, IssueId, OwnerControllerId, 1f, Array.Empty<byte>()));

        Other.Call(() =>
        {
            Assert.True(Other.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out var ownerControllerId));
            Assert.Equal(OwnerControllerId, ownerControllerId);
        });
    }

    [Fact]
    public void QuestSolution_AnotherClientGetsNoQuestAndIsNotTheSolver()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var otherIssue = AddIssue(Other, notableId);

        Other.SimulateMessage(this, new NetworkQuestSolutionAccepted(notableId, IssueId, OwnerControllerId, 1f, Array.Empty<byte>()));

        Other.Call(() =>
        {
            Assert.True(otherIssue.IsSolvingWithQuest);
            Assert.Null(otherIssue.IssueQuest);
            Assert.False(otherIssue.IsTriedToSolveBefore);
            Assert.Empty(Campaign.Current.QuestManager.Quests);
        });
    }

    [Fact]
    public void QuestSolution_TheEndingIsNotCreditedToAnotherClientsPlayer()
    {
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        AddIssue(Other, notableId);
        ListenForIssueUpdatesOnTheOtherClient();

        Other.SimulateMessage(this, new NetworkQuestSolutionAccepted(notableId, IssueId, OwnerControllerId, 1f, Array.Empty<byte>()));
        Other.SimulateMessage(this, new NetworkIssueFinalized(notableId, IssueId, IssueOutcome.QuestSuccess));

        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.Null(notable.Issue);
        });

        var update = Assert.Single(otherUpdates);
        Assert.Equal(IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess, update.Details);
        Assert.Null(update.Solver);
    }
}
