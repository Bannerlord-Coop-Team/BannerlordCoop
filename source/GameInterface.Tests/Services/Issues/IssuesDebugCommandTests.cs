using Autofac;
using Common;
using Common.Util;
using GameInterface.Services.Issues.Commands;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests;
using Serilog.Core;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

/// <summary>
/// Lightweight guard-clause coverage matching <c>HeroDebugCommandTests</c>'s own pattern - the real,
/// happy-path give/complete flows (a genuine <see cref="IssueManager.CreateNewIssue"/>/
/// <see cref="IssueManager.StartIssueQuest"/>/<see cref="TaleWorlds.CampaignSystem.QuestBase"/> completion
/// cascade) are covered end-to-end in <c>E2E.Tests/Services/Issues/IssuesDebugCommandTests.cs</c> instead,
/// where a real campaign fixture exists.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class IssuesDebugCommandTests : System.IDisposable
{
    public void Dispose()
    {
        ContainerProvider.Clear();
    }

    private static IObjectManager NewObjectManager() => new ObjectManager(Logger.None);

    private static void UseObjectManager(IObjectManager objectManager)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(objectManager).As<IObjectManager>();
        ContainerProvider.SetContainer(builder.Build());
    }

    private static Hero NewRegisteredHero(IObjectManager objectManager, string id)
    {
        var hero = ObjectHelper.SkipConstructor<Hero>();
        Assert.True(objectManager.AddExisting(id, hero));
        return hero;
    }

    // --- give: client rejection ---

    [Fact]
    public void Give_WhenClient_ReturnsServerOnlyError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var result = IssuesDebugCommand.Give(new List<string> { "some_hero", "BettingFraud" });

            Assert.Equal(
                "The 'coop.debug.issues.give' command cannot be used on the client. It is intended for server use only.",
                result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // --- give: missing hero ---

    [Fact]
    public void Give_WithUnknownHeroId_ReturnsNotFoundError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        UseObjectManager(NewObjectManager());

        try
        {
            var result = IssuesDebugCommand.Give(new List<string> { "no_such_hero", "BettingFraud" });

            Assert.Contains("No Hero found with id", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // --- give: unknown quest type key ---

    [Fact]
    public void Give_WithUnknownTypeKey_ReturnsUnknownKeyError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        var objectManager = NewObjectManager();
        UseObjectManager(objectManager);
        NewRegisteredHero(objectManager, "hero_1");

        try
        {
            var result = IssuesDebugCommand.Give(new List<string> { "hero_1", "NotARealQuestType" });

            Assert.Contains("Unknown quest type key 'NotARealQuestType'", result);
            Assert.Contains("list_types", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // --- give: known but not-wired quest type key ---

    [Fact]
    public void Give_WithKnownButNotWiredTypeKey_ReturnsNotWiredReason()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        var objectManager = NewObjectManager();
        UseObjectManager(objectManager);
        NewRegisteredHero(objectManager, "hero_1");

        try
        {
            var result = IssuesDebugCommand.Give(new List<string> { "hero_1", "ProdigalSon" });

            Assert.Contains("is a known vanilla Issue type but is not wired for give", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // --- give: hero already has an issue ---

    [Fact]
    public void Give_WhenHeroAlreadyHasAnIssue_ReturnsAlreadyHasIssueError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        var objectManager = NewObjectManager();
        UseObjectManager(objectManager);
        var hero = NewRegisteredHero(objectManager, "hero_1");
        var existingIssue = ObjectHelper.SkipConstructor<BettingFraudIssueBehavior.BettingFraudIssue>();
        hero.OnIssueCreatedForHero(existingIssue);

        try
        {
            var result = IssuesDebugCommand.Give(new List<string> { "hero_1", "BettingFraud" });

            Assert.Contains("already has an active issue", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // --- complete: client rejection ---

    [Fact]
    public void Complete_WhenClient_ReturnsServerOnlyError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = false;

        try
        {
            var result = IssuesDebugCommand.Complete(new List<string> { "some_hero" });

            Assert.Equal(
                "The 'coop.debug.issues.complete' command cannot be used on the client. It is intended for server use only.",
                result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // --- complete: missing hero ---

    [Fact]
    public void Complete_WithUnknownHeroId_ReturnsNotFoundError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        UseObjectManager(NewObjectManager());

        try
        {
            var result = IssuesDebugCommand.Complete(new List<string> { "no_such_hero" });

            Assert.Contains("No Hero found with id", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // --- complete: hero has no issue ---

    [Fact]
    public void Complete_WhenHeroHasNoIssue_ReturnsNoActiveIssueError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        var objectManager = NewObjectManager();
        UseObjectManager(objectManager);
        NewRegisteredHero(objectManager, "hero_1");

        try
        {
            var result = IssuesDebugCommand.Complete(new List<string> { "hero_1" });

            Assert.Contains("has no active issue", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    // --- complete: issue exists but no live quest yet ---

    [Fact]
    public void Complete_WhenIssueHasNoLiveQuestYet_ReturnsNoQuestError()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        var objectManager = NewObjectManager();
        UseObjectManager(objectManager);
        var hero = NewRegisteredHero(objectManager, "hero_1");
        var issue = ObjectHelper.SkipConstructor<BettingFraudIssueBehavior.BettingFraudIssue>();
        hero.OnIssueCreatedForHero(issue);

        try
        {
            var result = IssuesDebugCommand.Complete(new List<string> { "hero_1" });

            Assert.Contains("no live quest yet", result);
            Assert.Contains("coop.debug.issues.give", result);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
