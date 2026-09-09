using Common.Commands;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Commands;
using SandBox.Issues;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class IssuesDebugCommandTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public IssuesDebugCommandTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Give_BettingFraud_ThenComplete_ProducesALiveQuestThenClearsTheIssue()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var setupHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            setupHero.StayingInSettlement = settlement;

            var giveResult = Give(new List<string> { heroId, "BettingFraud" });
            Assert.True(giveResult.Contains("Gave quest type 'BettingFraud'"), giveResult);

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            var issue = Assert.IsType<BettingFraudIssueBehavior.BettingFraudIssue>(hero.Issue);
            Assert.NotNull(issue.IssueQuest);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(hero));

            var completeResult = Complete(new List<string> { heroId });
            Assert.Contains("Completed quest for hero", completeResult);

            Assert.Null(hero.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(hero));
        });
    }

    [Fact]
    public void Give_BettingFraud_Twice_RejectsTheSecondCallWithAlreadyHasIssueError()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var setupHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            setupHero.StayingInSettlement = settlement;

            Assert.Contains("Gave quest type", Give(new List<string> { heroId, "BettingFraud" }));

            var secondResult = Give(new List<string> { heroId, "BettingFraud" });

            Assert.Contains("already has an active issue", secondResult);
        });
    }

    [Fact]
    public void Give_VillageNeedsTools_ThenComplete_ProducesALiveQuestThenClearsTheIssue()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            settlement.SetSettlementComponent(village);
            village.Bound = settlement;
            village.Hearth = 650f;
            hero.StayingInSettlement = settlement;

            var giveResult = Give(new List<string> { heroId, "VillageNeedsTools" });
            Assert.True(giveResult.Contains("Gave quest type 'VillageNeedsTools'"), giveResult);

            var issue = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue>(hero.Issue);
            Assert.NotNull(issue.IssueQuest);
            Assert.Same(DefaultItems.Tools, issue._requestedItem);

            var completeResult = Complete(new List<string> { heroId, "cancel" });
            Assert.Contains("outcome 'cancel'", completeResult);

            Assert.Null(hero.Issue);
        });
    }

    [Fact]
    public void Give_RuralNotableInnAndOut_ThenComplete_ProducesALiveQuestThenClearsTheIssue()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var boundTownId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(boundTownId, out var boundTown));

            settlement.SetSettlementComponent(village);
            village.Bound = boundTown;
            boundTown.Culture = ObjectHelper.SkipConstructor<CultureObject>();
            hero.StayingInSettlement = settlement;

            var giveResult = Give(new List<string> { heroId, "RuralNotableInnAndOut" });
            Assert.Contains("Gave quest type 'RuralNotableInnAndOut'", giveResult);

            var issue = Assert.IsType<RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue>(hero.Issue);
            Assert.NotNull(issue.IssueQuest);

            var completeResult = Complete(new List<string> { heroId, "fail" });
            Assert.Contains("outcome 'fail'", completeResult);

            Assert.Null(hero.Issue);
        });
    }

    [Fact]
    public void Give_NotableWantsDaughterFound_ToHeroWithNoCurrentSettlement_RollsBackCleanlyInsteadOfStickingTheIssue()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Null(hero.CurrentSettlement);

            var giveResult = Give(new List<string> { heroId, "NotableWantsDaughterFound" });

            Assert.Contains("NotableWantsDaughterFound", giveResult);
            Assert.Contains("StartIssueQuest threw", giveResult);

            Assert.Null(hero.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(hero));

            hero.StayingInSettlement = settlement;

            var retryResult = Give(new List<string> { heroId, "BettingFraud" });
            Assert.Contains("Gave quest type 'BettingFraud'", retryResult);
        });
    }

    [Fact]
    public void ListTypes_ReportsAllFortyThreeVanillaIssueTypes_WithExactlyOneNotWired()
    {
        var result = ListTypes(new List<string>());
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(43, lines.Length);
        Assert.Single(lines, line => line.Contains("[not wired"));
        Assert.Contains(lines, line => line.StartsWith("ProdigalSon ") && line.Contains("[not wired"));
    }

    private static string Give(List<string> args)
    {
        var command = new IssuesDebugCommand.IssuesGiveCoopCommand();
        return command.ProcessCommand(new CoopCommandArgsFactory().FromValues(args)).Output;
    }

    private static string Complete(List<string> args)
    {
        var command = new IssuesDebugCommand.IssuesCompleteCoopCommand();
        return command.ProcessCommand(new CoopCommandArgsFactory().FromValues(args)).Output;
    }

    private static string ListTypes(List<string> args)
    {
        var command = new IssuesDebugCommand.IssuesListTypesCoopCommand();
        return command.ProcessCommand(new CoopCommandArgsFactory().FromValues(args)).Output;
    }

}
