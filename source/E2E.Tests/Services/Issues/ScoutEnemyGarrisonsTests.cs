using Autofac;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons;
using GameInterface.Services.Issues.Quests.ScoutEnemyGarrisons.Patches;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;
using QuestSettlement = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.QuestSettlement;

namespace E2E.Tests.Services.Issues;

public class ScoutEnemyGarrisonsTests : IDisposable
{
    private const string IssueName = "ScoutEnemyGarrisonsIssue";

    private readonly MethodCallRecorder scoutingDone;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public ScoutEnemyGarrisonsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scoutingDone = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.AllScoutingDone)));
    }

    public void Dispose()
    {
        scoutingDone.Dispose();
        TestEnvironment.Dispose();
    }

    [Fact]
    public void DebugCapture_IsRefusedForAHeroOutsideAKingdom()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.False(Descriptor(Server).CreationCaptureStrategy.TryBuildDebugCapture(hero, out _));
        });
    }

    [Fact]
    public void CreationCapture_BuildsTheClientIssueWithTheServerTargets()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var targetIds = CreateTargets();
        byte[] captured = null!;

        Server.Call(() =>
        {
            var issue = IssueWithTargets(Resolve(Server, targetIds));
            captured = Descriptor(Server).CreationCaptureStrategy.Capture(issue);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            var issue = (Issue)Descriptor(Client).CreationCaptureStrategy.CreateIssue(hero, captured);

            Assert.Equal(Resolve(Client, targetIds), new[] { issue._settlement1, issue._settlement2, issue._settlement3 });
        });
    }

    [Fact]
    public void CreationCapture_RefusesToBuildAnIssueWhoseTargetsAreUnknown()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.Throws<InvalidOperationException>(() => Descriptor(Client).CreationCaptureStrategy.CreateIssue(hero, Array.Empty<byte>()));
        });
    }

    [Fact]
    public void AcceptCapture_ForcesTheAccepterTargetsOntoTheMirror()
    {
        var accepterTargetIds = CreateTargets();
        var staleTargetIds = CreateTargets();

        Server.Call(() =>
        {
            var strategy = Descriptor(Server).QuestSolutionAcceptStrategy;
            var accepterTargets = Resolve(Server, accepterTargetIds);
            var mirrorIssue = IssueWithTargets(Resolve(Server, staleTargetIds));
            mirrorIssue.IssueQuest = QuestWithTargets(Resolve(Server, staleTargetIds));

            strategy.Apply(mirrorIssue, strategy.Capture(IssueWithTargets(accepterTargets)));

            var mirror = (Quest)mirrorIssue.IssueQuest;
            Assert.Equal(accepterTargets, new[] { mirrorIssue._settlement1, mirrorIssue._settlement2, mirrorIssue._settlement3 });
            Assert.Equal(accepterTargets, new[] { mirror._questSettlement1.Settlement, mirror._questSettlement2.Settlement, mirror._questSettlement3.Settlement });
            Assert.All(new[] { mirror._questSettlement1, mirror._questSettlement2, mirror._questSettlement3 },
                target => Assert.Equal(0, target.CurrentScoutProgress));
        });
    }

    [Fact]
    public void ScoutingBranch_RunsAllScoutingDoneOnTheServer()
    {
        Server.Call(() =>
        {
            var quest = QuestWithTargets(Resolve(Server, CreateTargets()));

            Assert.True(Descriptor(Server).FinalizationProofStrategy.TryRunBranch(quest, (byte)ScoutEnemyGarrisonsBranch.AllScoutingDone));
        });

        Assert.Equal(1, scoutingDone.CountFor(Server));
    }

    [Fact]
    public void ScoutingBranch_RefusesAnUnknownProof()
    {
        Server.Call(() =>
        {
            var quest = QuestWithTargets(Resolve(Server, CreateTargets()));

            Assert.False(Descriptor(Server).FinalizationProofStrategy.TryRunBranch(quest, 0));
        });

        Assert.Equal(0, scoutingDone.CountFor(Server));
    }

    [Fact]
    public void ClientScoutingDone_IsHandedToTheServerAndStopsTheQuestListeners()
    {
        var targetIds = CreateTargets();
        var requests = new List<QuestBranchRequested>();

        using (var logs = new MethodCallRecorder(AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog))))
        {
            Client.Call(() =>
            {
                Client.Resolve<IMessageBroker>().Subscribe<QuestBranchRequested>(payload => requests.Add(payload.What));

                var quest = QuestWithTargets(Resolve(Client, targetIds));
                Assert.True(ScoutEnemyGarrisonsHandedOffListenerPatch.Prefix(quest));

                quest.AllScoutingDone();

                Assert.False(ScoutEnemyGarrisonsHandedOffListenerPatch.Prefix(quest));
            });

            // The success entry waits for the server's outcome
            Assert.Equal(0, logs.CountFor(Client));
        }

        var request = Assert.Single(requests);
        Assert.Equal((byte)ScoutEnemyGarrisonsBranch.AllScoutingDone, request.Proof);
    }

    [Fact]
    public void ReplayedSuccess_AddsTheSuccessEntryToTheOwnerJournal()
    {
        var targetIds = CreateTargets();

        // The success text needs a fully built quest giver and finalizing needs a started quest, the test only counts the log entries
        using (new MethodCallRecorder(AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestSuccess))))
        using (new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))))
        using (var logs = new MethodCallRecorder(AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog))))
        {
            Client.Call(() =>
            {
                var quest = QuestWithTargets(Resolve(Client, targetIds));

                using (new AllowedThread())
                {
                    quest.CompleteQuestWithSuccess();
                }
            });

            Assert.Equal(1, logs.CountFor(Client));
        }
    }

    private string[] CreateTargets() => new[]
    {
        TestEnvironment.CreateRegisteredObject<Settlement>(),
        TestEnvironment.CreateRegisteredObject<Settlement>(),
        TestEnvironment.CreateRegisteredObject<Settlement>(),
    };

    private static Settlement[] Resolve(EnvironmentInstance instance, string[] ids) => ids
        .Select(id =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(id, out var settlement));
            return settlement;
        })
        .ToArray();

    private static IQuestTypeDescriptor Descriptor(EnvironmentInstance instance)
    {
        Assert.True(instance.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }

    private static Issue IssueWithTargets(Settlement[] targets)
    {
        var issue = (Issue)RuntimeHelpers.GetUninitializedObject(typeof(Issue));
        issue._settlement1 = targets[0];
        issue._settlement2 = targets[1];
        issue._settlement3 = targets[2];

        return issue;
    }

    private static Quest QuestWithTargets(Settlement[] targets)
    {
        var quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
        quest._questSettlement1 = new QuestSettlement(targets[0], 8);
        quest._questSettlement2 = new QuestSettlement(targets[1], 8);
        quest._questSettlement3 = new QuestSettlement(targets[2], 8);

        return quest;
    }
}
