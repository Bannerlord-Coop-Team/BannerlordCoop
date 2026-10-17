using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;
using HorseIssue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using HorseOwnership = GameInterface.Services.Issues.Generic.IIssueOwnershipRegistry;
using FrameworkOwnership = GameInterface.Services.Issues.Framework.Interface.IIssueOwnershipRegistry;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// The horse quest keeps its owners in its own registry, which tells the framework, so the framework's similar
/// commitments gate and accept preconditions treat it like any other quest: every player can hold one, nobody two.
/// </summary>
public class LordNeedsHorsesSimilarCommitmentsTests : IDisposable
{
    private const string ControllerA = "player-A";
    private const string ControllerB = "player-B";
    private const string FirstIssueId = "horse_first";
    private const string SecondIssueId = "horse_second";

    // Only the world requirements of taking the quest are stubbed, the similar commitments rules under test are real
    private readonly StubIssuePreconditions preconditions = new(typeof(HorseIssue));
    private readonly string firstNotableId;
    private readonly string secondNotableId;
    private readonly Player playerA;
    private readonly Player playerB;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public LordNeedsHorsesSimilarCommitmentsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        firstNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        secondNotableId = TestEnvironment.CreateRegisteredObject<Hero>();

        playerA = CreatePlayer(ControllerA);
        playerB = CreatePlayer(ControllerB);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(playerA));
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(playerB));
            });

            AddIssue(instance, firstNotableId, FirstIssueId);
            AddIssue(instance, secondNotableId, SecondIssueId);
        }

        Connect(ClientA, playerA);
        Connect(ClientB, playerB);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void ARecordedOwner_IsKnownToTheFramework()
    {
        Take(firstNotableId, ControllerA);

        foreach (var instance in Instances)
        {
            Assert.Equal(ControllerA, FrameworkOwner(instance, firstNotableId, FirstIssueId));
            Assert.Equal(ControllerA, ResolvedOwner(instance, firstNotableId));
        }
    }

    [Fact]
    public void AClearedOwner_IsForgottenByTheFramework()
    {
        Take(firstNotableId, ControllerA);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(firstNotableId, out var notable));
                instance.Resolve<HorseOwnership>().Clear(notable);
            });

            Assert.Null(FrameworkOwner(instance, firstNotableId, FirstIssueId));
        }
    }

    [Fact]
    public void EachPlayer_CanHoldAHorseQuestAtTheSameTime()
    {
        Take(firstNotableId, ControllerA);

        Assert.True(CheckPreconditions(ClientB, secondNotableId));
        Assert.True(CanTake(playerB, secondNotableId));

        Take(secondNotableId, ControllerB);

        Assert.Equal(ControllerA, FrameworkOwner(Server, firstNotableId, FirstIssueId));
        Assert.Equal(ControllerB, FrameworkOwner(Server, secondNotableId, SecondIssueId));
        Assert.False(CheckPreconditions(ClientA, secondNotableId));
        Assert.False(CheckPreconditions(ClientB, firstNotableId));
    }

    [Fact]
    public void ThePlayerHoldingAHorseQuest_IsRefusedASecondOne()
    {
        Take(firstNotableId, ControllerA);

        Assert.False(CheckPreconditions(ClientA, secondNotableId));
        Assert.False(CanTake(playerA, secondNotableId));
    }

    [Fact]
    public void AnEligiblePlayer_IsNotRefused()
    {
        Assert.True(CheckPreconditions(ClientA, firstNotableId));
        Assert.True(CheckPreconditions(ClientB, secondNotableId));
        Assert.True(CanTake(playerA, firstNotableId));
        Assert.True(CanTake(playerB, secondNotableId));
    }

    [Fact]
    public void ThePlayerHoldingAHorseQuest_IsRefusedASecondOneOnceAnotherPlayerHoldsOne()
    {
        Take(firstNotableId, ControllerA);
        Take(secondNotableId, ControllerB);

        Assert.False(CanTake(playerA, secondNotableId));
        Assert.False(CanTake(playerB, firstNotableId));
    }

    private Player CreatePlayer(string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        return new Player(controllerId, heroId, partyId, "", "");
    }

    private void Connect(EnvironmentInstance client, Player player)
    {
        TestEnvironment.ConnectRegisteredPlayer(client, player.ControllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(player.ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            Game.Current.PlayerTroop = hero.CharacterObject;
            Assert.Same(hero, Hero.MainHero);
        });
    }

    private static void AddIssue(EnvironmentInstance instance, string notableId, string issueId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

            using (new AllowedThread())
            {
                var issue = new HorseIssue(notable);
                issue.StringId = issueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });
    }

    // Every machine learns of an accepted horse quest through the horse quest's own registry
    private void Take(string notableId, string controllerId)
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));

                using (new AllowedThread())
                {
                    notable.Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                }

                instance.Resolve<HorseOwnership>().SetOwner(notable, controllerId);
            });
        }
    }

    private static bool CheckPreconditions(EnvironmentInstance client, string notableId)
    {
        var result = false;

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            result = notable.Issue.CheckPreconditions(notable, out _);
        });

        return result;
    }

    private bool CanTake(Player player, string notableId)
    {
        var result = false;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var hero));
            result = Server.Resolve<IIssueAcceptPreconditions>().CanTake(notable.Issue, hero, player.ControllerId);
        });

        return result;
    }

    private static string? FrameworkOwner(EnvironmentInstance instance, string notableId, string issueId)
    {
        string? controllerId = null;

        instance.Call(() =>
        {
            instance.Resolve<FrameworkOwnership>().TryGetOwner(notableId, issueId, out controllerId);
        });

        return controllerId;
    }

    private static string? ResolvedOwner(EnvironmentInstance instance, string notableId)
    {
        string? controllerId = null;

        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            instance.Resolve<IIssueOwnerResolver>().TryGetOwnerControllerId(notable.Issue, out controllerId);
        });

        return controllerId;
    }
}
