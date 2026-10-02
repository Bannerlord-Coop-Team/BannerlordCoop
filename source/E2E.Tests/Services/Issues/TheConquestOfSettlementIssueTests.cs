using Common.Util;
using E2E.Tests.Environment;
using Common.Commands;
using Coop.Core.Server.Services.Stances.Messages;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Issues.Commands;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.TheConquestOfSettlement;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Commands;
using Helpers;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

using Issue = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue;
using Quest = TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest;

public class TheConquestOfSettlementIssueTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;

    public TheConquestOfSettlementIssueTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

#if DEBUG
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EligibilityCannotBorrowAnotherPlayersMissingHeroOrParty(bool missingHero)
    {
        var created = CreateIssue();
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var controller = "missing-object-player";
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controller,
                missingHero ? "missing-hero" : heroId, missingHero ? partyId : "missing-party", "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
            var issue = giver.Issue;
            var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
            var previousParty = Campaign.Current.MainParty;

            var result = new ConquestQuestCommands.Eligibility().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { created.GiverId, controller }));

            Assert.False(result.Succeeded);
            Assert.Same(issue, giver.Issue);
            Assert.True(issue.IsOngoingWithoutQuest);
            Assert.Null(issue.IssueQuest);
            Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
            Assert.Same(previousParty, Campaign.Current.MainParty);
        });
    }

    [Fact]
    public void JournalCannotOpenOnTheDedicatedAuthority()
    {
        Server.Call(() =>
        {
            var result = new ConquestQuestCommands.Journal().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { "open" }));
            Assert.False(result.Succeeded);
        });
    }
#endif

    [Fact]
    public void RoutingSendsOnlyTheValidatedMapEventToTheMissionAuthority()
    {
        var id = environment.CreateRegisteredObject<MapEvent>();
        var command = new MapEventDebugCommands.RouteBattleEnemiesCoopCommand();
        var args = new CoopCommandArgsFactory();
        foreach (var client in environment.Clients)
            client.Call(() => Assert.False(command.ProcessCommand(args.FromValues(new[] { id, "0" })).Succeeded));
        Server.Call(() =>
        {
            Assert.False(command.ProcessCommand(args.FromValues(new[] { id, "-1" })).Succeeded);
            Assert.False(command.ProcessCommand(args.FromValues(new[] { "missing-event", "0" })).Succeeded);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRouteBattleEnemies>());
            Assert.True(command.ProcessCommand(args.FromValues(new[] { id, "3" })).Succeeded);
        });
        var request = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRouteBattleEnemies>());
        Assert.Equal(id, request.MapEventId);
        Assert.Equal(3, request.EnemiesToLeaveFighting);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WarCommandCannotBorrowAnotherPlayersMissingHeroOrParty(bool missingHero)
    {
        var faction1Id = environment.CreateRegisteredObject<Kingdom>();
        var faction2Id = environment.CreateRegisteredObject<Kingdom>();
        var heroId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var controller = "missing-war-actor";
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controller,
                missingHero ? "missing-hero" : heroId, missingHero ? partyId : "missing-party", "", "")));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(faction1Id, out var faction1));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(faction2Id, out var faction2));
            var previousHero = ResolvedMainHeroContext.ResolvedMainHero;
            var previousParty = Campaign.Current.MainParty;

            var result = new KingdomDebugCommand.KingdomDeclareWarCoopCommand().ProcessCommand(
                new CoopCommandArgsFactory().FromValues(new[] { faction1Id, faction2Id, "hostility", controller }));

            Assert.False(result.Succeeded);
            Assert.Contains("registered hero and party", result.Output);
            Assert.False(FactionManager.IsAtWarAgainstFaction(faction1, faction2));
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
            Assert.Same(previousHero, ResolvedMainHeroContext.ResolvedMainHero);
            Assert.Same(previousParty, Campaign.Current.MainParty);
        });
    }

    private NetworkConquestIssueCreated CreateIssue()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var targetId = environment.CreateRegisteredObject<Settlement>();
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    if (instance != Server) Campaign.Current.IssueManager._nextIssueUniqueIndex = 200;
                }
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) => new Issue(owner, target),
                typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        return Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkConquestIssueCreated>());
    }

    [Fact]
    public void CreationKeepsTheServerTargetDeadlineAndIdentityAcrossDifferentClientCounters()
    {
        var created = CreateIssue();
        Assert.StartsWith("coop_conquest_issue_", created.IssueId);
        foreach (var instance in new[] { Server }.Concat(environment.Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.Same(target, issue._targetSettlement);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Equal(created.DueTime, issue.IssueDueTime);
                Assert.True(instance.Resolve<IIssueGenerationRegistry>().TryGetGeneration(giver, out var generation));
                Assert.Equal(created.Generation, generation);
            });
        }
    }

    [Fact]
    public void DelayedCreationCannotReplaceTheCurrentIssue()
    {
        var created = CreateIssue();
        var differentTargetId = environment.CreateRegisteredObject<Settlement>();
        foreach (var client in environment.Clients)
        {
            client.SimulateMessage(this, new NetworkConquestIssueCreated(created.GiverId, differentTargetId,
                created.Generation - 1, CampaignTime.Never, "old-issue"));
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
                var issue = Assert.IsType<Issue>(giver.Issue);
                Assert.Same(target, issue._targetSettlement);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Equal(created.DueTime, issue.IssueDueTime);
            });
        }
    }

    [Theory]
    [InlineData("owner", true)]
    [InlineData("other", false)]
    [InlineData("unknown", false)]
    public void HostilityUsesTheInitiatorCapturedBeforeQuestOwnerSubstitution(string initiator, bool causedByOwner)
    {
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var otherId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherId, out var other));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var wasApplying = ConquestQuestEventPatches.IsApplying;
            var previousTrigger = ConquestQuestEventPatches.TriggeringHero;
            using (new MainHeroSubstitutionScope(owner, party))
            {
                try
                {
                    ConquestQuestEventPatches.IsApplying = true;
                    ConquestQuestEventPatches.TriggeringHero = initiator == "owner" ? owner : initiator == "other" ? other : null;
                    Assert.Equal(causedByOwner, DiplomacyHelper.IsWarCausedByPlayer(null, null,
                        DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility));
                }
                finally
                {
                    ConquestQuestEventPatches.TriggeringHero = previousTrigger;
                    ConquestQuestEventPatches.IsApplying = wasApplying;
                }
            }
        });
    }

    [Fact]
    public void PlayerRemovalCancelsOnlyItsQuestEvenAfterThePlayerObjectsAreGone()
    {
        var created = CreateIssue();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(created.GiverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(created.TargetId, out var target));
            var quest = new Quest(created.IssueId + "_quest", giver, target, CampaignTime.DaysFromNow(60), 20000);
            var unrelated = ObjectHelper.SkipConstructor<Quest>();
            unrelated.StringId = "another_players_quest";
            var ownership = Server.Resolve<IIssueOwnershipRegistry>();
            using (new AllowedThread())
            {
                giver.Issue.IssueQuest = quest;
                giver.Issue.IsTriedToSolveBefore = true;
                giver.Issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                Campaign.Current.QuestManager.OnQuestStarted(quest);
                Campaign.Current.QuestManager.OnQuestStarted(unrelated);
            }
            ownership.SetOwner(giver, "removed-player");
            ownership.SetQuestOwner(quest.StringId, "removed-player");
            ownership.SetQuestOwner(unrelated.StringId, "another-player");

            Server.Resolve<IConquestQuest>().CancelForPlayerRemoval("removed-player");

            Assert.True(quest.IsFinalized);
            Assert.Null(giver.Issue);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            Assert.Contains(unrelated, Campaign.Current.QuestManager.Quests);
            Assert.False(ownership.TryGetOwnerControllerId(giver, out _));
            Assert.Null(ConquestQuest.CancellingRemovedPlayerQuest);
            Assert.True(ownership.TryGetQuestOwner(quest.StringId, out var historicalOwner));
            Assert.Equal("removed-player", historicalOwner);
            Campaign.Current.QuestManager.OnQuestFinalized(unrelated);
        });
    }
}
