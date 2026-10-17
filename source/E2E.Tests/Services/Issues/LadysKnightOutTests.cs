using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Quests.LadysKnightOut;
using GameInterface.Services.Players;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Localization;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LadysKnightOutIssueBehavior.LadysKnightOutIssueQuest;

namespace E2E.Tests.Services.Issues;

public class LadysKnightOutTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly List<(EnvironmentInstance Instance, HeroCreationModel Model)> models = new();
    private IEnumerable<EnvironmentInstance> Instances => environment.Clients.Prepend(environment.Server);

    public LadysKnightOutTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                models.Add((instance, Campaign.Current.Models.HeroCreationModel));
                var creation = new Mock<HeroCreationModel>();
                creation.Setup(model => model.IsHeroCombatant(It.IsAny<Hero>())).Returns(false);
                creation.Setup(model => model.GenerateFirstAndFullName(It.IsAny<Hero>()))
                    .Returns((new TextObject("Test"), new TextObject("Test Lady")));
                Campaign.Current.Models.HeroCreationModel = creation.Object;
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                var giver = instance.GetRegisteredObject<Hero>(scenario.NotableId);
                var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                var town = instance.GetRegisteredObject<Town>(townId);
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    town.OwnerClan = giver.Clan;
                    town.Security = 40;
                    giver.StayingInSettlement = settlement;
                    giver.Occupation = Occupation.Lord;
                    giver.IsFemale = true;
                    var issue = new Issue(giver) { StringId = QuestScenario.IssueId };
                    Campaign.Current.IssueManager._issues[giver] = issue;
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }
    }

    public void Dispose()
    {
        foreach (var (instance, model) in models)
            instance.Call(() => Campaign.Current.Models.HeroCreationModel = model);
        environment.Dispose();
    }

    private Quest Accept(EnvironmentInstance client, float difficulty = 0)
    {
        client.Call(() =>
        {
            using (new IssueDifficultyOverride(difficulty)) scenario.Accept(client);
            ((Quest)scenario.QuestOf(client)).QuestAcceptedConsequences();
        });
        return (Quest)scenario.QuestOf(client);
    }

    [Fact]
    public void RealAcceptanceCreatesOnlyTheOwnersJournalAndAnUnstartedServerQuest()
    {
        var quest = Accept(scenario.ClientA);
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        scenario.ClientA.Call(() => Assert.Single(quest.JournalEntries));
        scenario.ClientB.Call(() => Assert.Null(scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue.IssueQuest));
        scenario.Server.Call(() =>
        {
            var serverQuest = (Quest)scenario.QuestOf(scenario.Server);
            Assert.Empty(serverQuest.JournalEntries);
            Assert.DoesNotContain(serverQuest, Campaign.Current.QuestManager.Quests);
        });
    }

    [Fact]
    public void ServerCreatesTheRealOfferWithTheSameIdentityOnBothClients()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = instance.GetRegisteredObject<Hero>(giverId);
                    giver.StayingInSettlement = instance.GetRegisteredObject<Hero>(scenario.NotableId).CurrentSettlement;
                    giver.Occupation = Occupation.Lord;
                    giver.IsFemale = true;
                }
            });
        scenario.Server.Call(() =>
        {
            var giver = scenario.Server.GetRegisteredObject<Hero>(giverId);
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero hero) => new Issue(hero), typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        var created = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.False(string.IsNullOrEmpty(created.IssueId));
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var issue = Assert.IsType<Issue>(instance.GetRegisteredObject<Hero>(giverId).Issue);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.True(issue.IsOngoingWithoutQuest);
                Assert.False(issue.IsThereAlternativeSolution);
                Assert.True(issue.IsThereLordSolution);
            });
    }

    [Theory]
    [InlineData(0f, 0, false, IssueOutcome.QuestFail)]
    [InlineData(0f, 1, false, IssueOutcome.QuestSuccess)]
    [InlineData(0.3f, 1, false, IssueOutcome.QuestFail)]
    [InlineData(0.3f, 2, false, IssueOutcome.QuestSuccess)]
    [InlineData(0.6f, 2, false, IssueOutcome.QuestFail)]
    [InlineData(0.6f, 3, false, IssueOutcome.QuestSuccess)]
    [InlineData(0.8f, 3, false, IssueOutcome.QuestFail)]
    [InlineData(0.8f, 3, true, IssueOutcome.QuestSuccess)]
    public void TournamentResultRunsTheRealBranchAsItsOwner(float difficulty, int round, bool champion, IssueOutcome expected)
    {
        var quest = Accept(scenario.ClientA, difficulty);
        int initialGold = 0;
        float initialRenown = 0;
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IIssueOwnerResolver>().TryResolveOwnerHero(
                scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue, out var owner));
            initialGold = owner.Gold;
            initialRenown = owner.Clan.Renown;
            scenario.Server.Resolve<IMessageBroker>().Publish(this, new TournamentSessionUpdated(Snapshot(round, false, champion)));
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        scenario.Server.Call(() => scenario.Server.Resolve<IMessageBroker>().Publish(this,
            new TournamentSessionUpdated(Snapshot(round, true, champion))));
        Assert.Equal(expected, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        environment.FlushCoalescer();
        scenario.ClientA.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Equal(2, quest.JournalEntries.Count);
        });
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            var hero = scenario.Server.GetRegisteredObject<Hero>(player.HeroId);
            Assert.Equal(initialGold + (expected == IssueOutcome.QuestSuccess ? 750 : 0), hero.Gold);
            Assert.Equal(initialRenown + (expected == IssueOutcome.QuestSuccess ? 1 : 0), hero.Clan.Renown);
        });
        foreach (var instance in Instances)
            instance.Call(() => Assert.Null(instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
    }

    [Fact]
    public void AnotherCompetitorsResultDoesNotFinishTheQuest()
    {
        Accept(scenario.ClientB);
        scenario.Server.Call(() =>
        {
            var broker = scenario.Server.Resolve<IMessageBroker>();
            broker.Publish(this, new TournamentSessionUpdated(Snapshot(3, false, true)));
            broker.Publish(this, new TournamentSessionUpdated(Snapshot(3, true, true)));
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void AClientCannotClaimATournamentWinWithoutAServerResult()
    {
        var quest = Accept(scenario.ClientA);
        scenario.ClientA.Call(() => quest.PlayerReachedTournamentRoundGoal());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        scenario.ClientA.Call(() => Assert.True(quest.IsOngoing));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrameworkAllowsSeparatePlayersButRefusesASecondQuestForTheSamePlayer(bool samePlayer)
    {
        Accept(scenario.ClientA);
        var secondGiverId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var first = instance.GetRegisteredObject<Hero>(scenario.NotableId);
                    var giver = instance.GetRegisteredObject<Hero>(secondGiverId);
                    giver.StayingInSettlement = first.CurrentSettlement;
                    giver.Occupation = Occupation.Lord;
                    giver.IsFemale = true;
                    var issue = new Issue(giver) { StringId = "second_lady" };
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }
        var client = samePlayer ? scenario.ClientA : scenario.ClientB;
        client.Call(() =>
        {
            var issue = client.GetRegisteredObject<Hero>(secondGiverId).Issue;
            issue.StartIssueWithQuest();
            if (!samePlayer) ((Quest)issue.IssueQuest).QuestAcceptedConsequences();
        });
        Assert.Equal(samePlayer ? 1 : 2, scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>().Count());
        if (samePlayer) Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        else
        {
            scenario.ClientA.Call(() => Assert.Null(scenario.ClientA.GetRegisteredObject<Hero>(secondGiverId).Issue.IssueQuest));
            scenario.ClientB.Call(() => Assert.Null(scenario.ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue.IssueQuest));
        }
    }

    [Fact]
    public void ServerRefusesAStaleOfferWhenSecurityHasMadeItIneligible()
    {
        scenario.Server.Call(() =>
        {
            using (new AllowedThread()) scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).CurrentSettlement.Town.Security = 81;
        });
        scenario.Accept(scenario.ClientA);
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
    }

    [Fact]
    public void CrimePenaltyChangesOnlyTheOwnersHonorStore()
    {
        foreach (var client in environment.Clients)
            client.Call(() =>
            {
                Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();
                Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Honor, 83);
            });
        Accept(scenario.ClientA);
        scenario.Server.Call(() => scenario.Server.Resolve<IOutcomeDispatcher>().RunAsOwners<Quest>(quest => quest.CriminalActionPerformedTowardsSettlement()));
        var penalty = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutHonor>());
        Assert.Equal(QuestScenario.ControllerA, penalty.ControllerId);
        scenario.ClientA.Call(() => Assert.Equal(penalty.Xp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
        scenario.ClientB.Call(() => Assert.Equal(83, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Fact]
    public void TimeoutRunsTheOwnersPenaltyAndJournalOnTheServer()
    {
        var quest = Accept(scenario.ClientA);
        scenario.ClientA.Call(() => quest.CompleteQuestWithTimeOut());
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        scenario.ClientA.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Equal(2, quest.JournalEntries.Count);
            Assert.Contains("failed to enter", quest.JournalEntries.Last().LogText.ToString());
        });
    }

    [Fact]
    public void GiverDeathCancelsFromTheServerEventWithoutRewardingAnotherPlayer()
    {
        var quest = Accept(scenario.ClientA);
        scenario.Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(
            scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId), null,
            KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false));
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        scenario.ClientA.Call(() => Assert.Equal(2, quest.JournalEntries.Count));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LordSolutionChargesOnlyTheAcceptedPlayersInfluenceAndCompletesOnce(bool enoughInfluence)
    {
        string heroId = null!;
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            heroId = player.HeroId;
        });
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var hero = instance.GetRegisteredObject<Hero>(heroId);
                using (new AllowedThread())
                {
                    hero.Clan.Influence = enoughInfluence ? 100 : 0;
                    instance.GetRegisteredObject<Hero>(scenario.NotableId).CurrentSettlement.Town.OwnerClan = hero.Clan;
                }
            });
        }
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.StartIssueWithLordSolution());
        if (!enoughInfluence)
        {
            Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutLordAccepted>());
            return;
        }
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkLadysKnightOutLordAccepted>());
        float expectedInfluence = 0;
        scenario.Server.Call(() => expectedInfluence = 100 - scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue.NeededInfluenceForLordSolution);
        scenario.ClientA.Call(() => scenario.ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        environment.FlushCoalescer();
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            Assert.Equal(expectedInfluence, scenario.Server.GetRegisteredObject<Hero>(player.HeroId).Clan.Influence);
        });
    }

    private static TournamentSessionSnapshot Snapshot(int round, bool finished, bool champion)
    {
        var contestant = new TournamentContestantData("slot", "hero", 1, QuestScenario.ControllerA, "Champion", true, false, false, null);
        var team = new TournamentTeamData("team", new[] { "slot" }, 0, champion, 0, null);
        var match = new TournamentMatchData("match", "round", finished ? (int)TournamentMatch.MatchState.Finished : (int)TournamentMatch.MatchState.Ready,
            1, 1, new[] { team }, champion ? new[] { "slot" } : new[] { "opponent" });
        return new TournamentSessionSnapshot("session", "mission", "town", "arena", "prize",
            finished ? TournamentSessionPhase.Completed : TournamentSessionPhase.LiveMatch,
            finished ? 2 : 1, finished ? 1 : 0, "match", QuestScenario.ControllerA, Array.Empty<string>(), new[] { contestant },
            Array.Empty<string>(), new[] { new TournamentPlayerChoiceData(QuestScenario.ControllerA, TournamentPlayerChoice.Join) },
            new[] { new TournamentRoundData("round", round, 0, new[] { match }) }, 1, 0, 1, false, finished && champion, champion ? "slot" : null);
    }
}
