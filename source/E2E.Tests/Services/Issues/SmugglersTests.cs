using Autofac;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.Smugglers;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace E2E.Tests.Services.Issues;

public class SmugglersTests : IDisposable
{
    private const string IssueName = "SmugglersIssue";
    private const string IssueId = "issue_smugglers";
    private const string ControllerA = "player-A";
    private const string ControllerB = "player-B";
    private const int RewardGold = 1000;
    private const int BribeAmount = 750;

    private readonly MethodCallRecorder succeedQuest;
    private readonly MethodCallRecorder successLogs;
    private readonly MethodCallRecorder hourlyTickParty;

    private readonly string issueOwnerId;
    private readonly string partyId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();

    public SmugglersTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        // Last, so the patches under test decide before the recorder skips the original
        succeedQuest = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.SucceedQuest)));
        hourlyTickParty = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.OnHourlyTickParty)));

        // The success logs read the quest giver's character and the town name, which these quests leave unset
        successLogs = new MethodCallRecorder(
            Priority.Last,
            AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestSuccessWithPersuasionLog)),
            AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestSuccessWithBribeLog)));

        issueOwnerId = TestEnvironment.CreateRegisteredObject<Hero>();
        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        RegisterPlayer(ClientA, ControllerA);
        RegisterPlayer(ClientB, ControllerB);

        AddIssue(Server, withQuest: true);
        AddIssue(ClientA, withQuest: true);
        AddIssue(ClientB, withQuest: false);

        Server.Call(() => Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(issueOwnerId, IssueId, ControllerA)));
    }

    public void Dispose()
    {
        succeedQuest.Dispose();
        hourlyTickParty.Dispose();
        successLogs.Dispose();
        TestEnvironment.Dispose();
    }

    [Fact]
    public void DebugCapture_IsRefusedForAHeroThatIsNotALord()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var hero));

            Assert.False(Descriptor(Server).CreationCaptureStrategy.TryBuildDebugCapture(hero, out _));
        });
    }

    [Fact]
    public void CreationCapture_RebuildsTheIssueBetweenTheSameTowns()
    {
        var targetId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var originId = TestEnvironment.CreateRegisteredObject<Settlement>();
        byte[] captured = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(originId, out var origin));

            using (new AllowedThread())
            {
                captured = Descriptor(Server).CreationCaptureStrategy.Capture(new Issue(issueOwner, new KeyValuePair<Settlement, Settlement>(target, origin)));
            }
        });

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
            Assert.True(ClientA.ObjectManager.TryGetObject<Settlement>(targetId, out var target));
            Assert.True(ClientA.ObjectManager.TryGetObject<Settlement>(originId, out var origin));

            Issue issue;
            using (new AllowedThread())
            {
                issue = (Issue)Descriptor(ClientA).CreationCaptureStrategy.CreateIssue(issueOwner, captured);
            }

            Assert.Same(target, issue._targetSettlement);
            Assert.Same(origin, issue._originSettlement);
        });
    }

    [Fact]
    public void Persuasion_IsRunOnTheServerForTheOwner()
    {
        ClientA.Call(() => QuestOf(ClientA).SucceedQuest(null));

        Assert.Equal(1, succeedQuest.CountFor(Server));
    }

    [Fact]
    public void Persuasion_FromAPlayerThatDoesNotOwnTheQuest_IsNotRun()
    {
        AttachQuest(ClientB);

        ClientB.Call(() => QuestOf(ClientB).SucceedQuest(null));

        Assert.Equal(0, succeedQuest.CountFor(Server));
    }

    [Fact]
    public void Bribe_IsNotRunWhenTheOwnerCannotPay()
    {
        var gold = RunBribeAsOwner(ownerGold: BribeAmount - 1);

        Assert.Equal(BribeAmount - 1, gold);
        Assert.Equal(0, succeedQuest.CountFor(Server));
    }

    [Fact]
    public void Bribe_IsPaidByTheOwner()
    {
        var gold = RunBribeAsOwner(ownerGold: BribeAmount + 10);

        Assert.Equal(10, gold);
        Assert.Equal(1, succeedQuest.CountFor(Server));
    }

    [Fact]
    public void PartyCreated_PointsOnlyTheOwnersQuestAtTheParty()
    {
        Server.Call(() => Server.Resolve<INetwork>().SendAll(new NetworkSmugglersPartyCreated(issueOwnerId, IssueId, partyId)));

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(party, QuestOf(ClientA)._smugglerParty);
        });
        ClientB.Call(() => Assert.Null(IssueOf(ClientB).IssueQuest));
    }

    [Fact]
    public void ClientAccept_UsesTheServersPartyInsteadOfSpawningOne()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var quest = QuestOf(ClientA);

            Assert.Null(quest.CreateSmugglerParty());

            quest._smugglerParty = party;

            Assert.Same(party, quest.CreateSmugglerParty());
        });
    }

    [Fact]
    public void SmugglersRoute_IsRunOnTheServerOnly()
    {
        TickParty(Server, usedByQuest: true);
        TickParty(ClientA, usedByQuest: true);

        Assert.Equal(1, hourlyTickParty.CountFor(Server));
        Assert.Equal(0, hourlyTickParty.CountFor(ClientA));
    }

    [Fact]
    public void SmugglersRoute_SkipsPartiesNotUsedByAQuest()
    {
        TickParty(Server, usedByQuest: false);

        Assert.Equal(0, hourlyTickParty.CountFor(Server));
    }

    private int RunBribeAsOwner(int ownerGold)
    {
        var gold = 0;

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(ControllerA, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(owner.HeroId, out var ownerHero));

            using (new AllowedThread())
            {
                ownerHero.ChangeHeroGold(ownerGold - ownerHero.Gold);
            }

            using (new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo))
            {
                Assert.True(Descriptor(Server).FinalizationProofStrategy.TryRunBranch(QuestOf(Server), (byte)SmugglersBranch.SuccessByBribe));
            }

            gold = ownerHero.Gold;
        });

        return gold;
    }

    private void TickParty(EnvironmentInstance instance, bool usedByQuest)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                party.SetPartyUsedByQuest(usedByQuest);
            }

            CampaignEventDispatcher.Instance.HourlyTickParty(party);
        });
    }

    private void RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var playerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, playerPartyId, "", ""))));
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
    }

    private void AddIssue(EnvironmentInstance instance, bool withQuest)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));

            using (new AllowedThread())
            {
                var issue = new Issue(issueOwner, new KeyValuePair<Settlement, Settlement>(null!, null!));
                issue.StringId = IssueId;

                Campaign.Current.IssueManager._issues.Add(issueOwner, issue);
                issueOwner.OnIssueCreatedForHero(issue);
            }
        });

        if (withQuest)
        {
            AttachQuest(instance);
        }
    }

    private void AttachQuest(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            var issue = IssueOf(instance);
            var quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
            quest.QuestGiver = issue.IssueOwner;
            AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold)).SetValue(quest, RewardGold);

            using (new AllowedThread())
            {
                issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                issue.IssueQuest = quest;
            }
        });
    }

    private IssueBase IssueOf(EnvironmentInstance instance)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(issueOwnerId, out var issueOwner));
        return issueOwner.Issue;
    }

    private Quest QuestOf(EnvironmentInstance instance)
    {
        return Assert.IsType<Quest>(IssueOf(instance).IssueQuest);
    }

    private static IQuestTypeDescriptor Descriptor(EnvironmentInstance instance)
    {
        Assert.True(instance.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }
}
