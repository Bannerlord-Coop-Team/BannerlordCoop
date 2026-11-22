using Autofac;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ArmyNeedsSupplies;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace E2E.Tests.Services.Issues;

public class ArmyNeedsSuppliesTests : IDisposable
{
    private const string IssueName = "ArmyNeedsSuppliesIssue";
    private const int RequestedGrain = 5;

    private readonly MethodCallRecorder grainBranch;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public ArmyNeedsSuppliesTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        grainBranch = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.CollectedGrainConsequence)));
    }

    public void Dispose()
    {
        grainBranch.Dispose();
        TestEnvironment.Dispose();
    }

    [Fact]
    public void DebugCapture_IsRefusedForAHeroThatDoesNotLeadAnArmy()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.False(Descriptor().CreationCaptureStrategy.TryBuildDebugCapture(hero, out _));
        });
    }

    [Fact]
    public void AcceptCapture_ForcesTheAccepterRequestedAmountsOntoTheMirror()
    {
        Server.Call(() =>
        {
            var strategy = Descriptor().QuestSolutionAcceptStrategy;
            var accepterIssue = IssueWithQuest(grain: 7, liveStock: 2, wine: 3);
            var mirrorIssue = IssueWithQuest(grain: 1, liveStock: 1, wine: 1);

            strategy.Apply(mirrorIssue, strategy.Capture(accepterIssue));

            var mirror = (Quest)mirrorIssue.IssueQuest;
            Assert.Equal(7, mirror._requestedGrainAmount);
            Assert.Equal(2, mirror._requestedLiveStockAmount);
            Assert.Equal(3, mirror._requestedWineAmount);
        });
    }

    [Fact]
    public void GrainBranch_IsNotRunWhenTheOwnerDoesNotCarryTheGrain()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        RunGrainBranchAsOwner(partyId, carriedGrain: RequestedGrain - 1);

        Assert.Equal(0, grainBranch.CountFor(Server));
    }

    [Fact]
    public void GrainBranch_RunsWhenTheOwnerCarriesTheGrain()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        RunGrainBranchAsOwner(partyId, carriedGrain: RequestedGrain);

        Assert.Equal(1, grainBranch.CountFor(Server));
    }

    private void RunGrainBranchAsOwner(string partyId, int carriedGrain)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                party.ItemRoster.Clear();
                party.ItemRoster.AddToCounts(DefaultItems.Grain, carriedGrain);
            }

            var quest = (Quest)IssueWithQuest(grain: RequestedGrain, liveStock: 0, wine: 0).IssueQuest;
            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = party;

            try
            {
                Assert.True(Descriptor().FinalizationProofStrategy.TryRunBranch(quest, (byte)ArmyNeedsSuppliesBranch.SuccessByGrain));
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }
        });
    }

    private IQuestTypeDescriptor Descriptor()
    {
        Assert.True(Server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }

    private static Issue IssueWithQuest(int grain, int liveStock, int wine)
    {
        var quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
        quest._requestedGrainAmount = grain;
        quest._requestedLiveStockAmount = liveStock;
        quest._requestedWineAmount = wine;

        var issue = (Issue)RuntimeHelpers.GetUninitializedObject(typeof(Issue));
        issue.IssueQuest = quest;

        return issue;
    }
}
