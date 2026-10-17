using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace E2E.Tests.Services.Issues;

public class ArmyNeedsSuppliesWarDeclaredTests : IDisposable
{
    private const string IssueId = "issue_army_needs_supplies";

    private static readonly MethodBase[] WarLogTexts =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerDeclaredWarQuestLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestCanceledWarDeclaredLog)),
    };

    private readonly string lordId;
    private readonly string lordClanId;
    private readonly string kingdomId;
    private readonly string armyPartyId;
    private readonly string playerClanId;
    private readonly string ownerHeroId;
    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public ArmyNeedsSuppliesWarDeclaredTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        lordId = TestEnvironment.CreateRegisteredObject<Hero>();
        lordClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        armyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        ownerHeroId = RegisterPlayer(ClientA, "player-A");
        RegisterPlayer(ClientB, "player-B");

        foreach (var instance in Instances)
        {
            GiveTheArmyLeaderTheIssue(instance);
        }

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
            lord.Issue.StartIssueWithQuest();
        });
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Theory]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByRebellion, IssueOutcome.QuestCancel)]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, IssueOutcome.QuestFail)]
    public void AWarDeclaredOnTheServerEndsTheQuestEverywhere(DeclareWarAction.DeclareWarDetail detail, IssueOutcome outcome)
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));

                using (new AllowedThread())
                {
                    ownerHero.Clan = playerClan;
                }
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));

            DeclareWarAction.ApplyInternal(kingdom, playerClan, detail);
        }, WarLogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(outcome, finalized.Outcome);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
                Assert.Null(lord.Issue);
            });
        }

        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(lordId, IssueId, out _));
        });
    }

    [Fact]
    public void AWarReplayedOnTheOwnersClientLeavesTheQuestToTheServer()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
            Assert.True(ClientA.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(ClientA.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
            var quest = Assert.IsType<Quest>(lord.Issue.IssueQuest);

            using (new AllowedThread())
            {
                Hero.MainHero.Clan = playerClan;
                FactionManager.DeclareWar(kingdom, playerClan);
                quest.OnWarDeclared(kingdom, playerClan, DeclareWarAction.DeclareWarDetail.CausedByRebellion);
            }

            Assert.True(quest.IsOngoing);
        }, WarLogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
                Assert.NotNull(lord.Issue);
            });
        }
    }

    private string RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", ""))));
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        return heroId;
    }

    private void GiveTheArmyLeaderTheIssue(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(lordId, out var lord));
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(lordClanId, out var lordClan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(armyPartyId, out var armyParty));

            using (new AllowedThread())
            {
                lord.Clan = lordClan;
                lordClan._kingdom = kingdom;
                lord._partyBelongedTo = armyParty;
                new Army(kingdom, armyParty, Army.ArmyTypes.Patrolling).ArmyOwner = lord;

                var issue = new Issue(lord);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(lord, issue);
                lord.OnIssueCreatedForHero(issue);
            }
        });
    }
}
