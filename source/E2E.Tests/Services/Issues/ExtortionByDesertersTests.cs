using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace E2E.Tests.Services.Issues;

/// <summary>Exercises the real issue through the framework with two independent player clients.</summary>
public class ExtortionByDesertersTests : IDisposable
{
    private static readonly MethodBase[] TextLinks =
    {
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
        AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.EncyclopediaLinkWithName)),
    };
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly MethodCallRecorder creation;
    private readonly string villageId;
    private readonly string townId;
    private readonly string townComponentId;
    private readonly string settlementId;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Other => scenario.ClientB;

    public ExtortionByDesertersTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        // Navigation and party templates need the native map, the framework path does not.
        creation = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.CreateDeserterParty)));
        villageId = environment.CreateRegisteredObject<Village>();
        townId = environment.CreateRegisteredObject<Settlement>();
        townComponentId = environment.CreateRegisteredObject<Town>();
        settlementId = environment.CreateRegisteredObject<Settlement>();
        scenario = new QuestScenario(environment, notable => new Issue(notable), Prepare);
    }

    public void Dispose()
    {
        creation.Dispose();
        environment.Dispose();
    }

    private void Prepare(EnvironmentInstance instance, Hero notable)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townId, out var town));
        Assert.True(instance.ObjectManager.TryGetObject<Town>(townComponentId, out var townComponent));
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
        village.Bound = town;
        town.SetSettlementComponent(townComponent);
        town.Town.Security = 30;
        settlement.SetSettlementComponent(village);
        notable.StayingInSettlement = settlement;
        notable.Occupation = Occupation.Headman;
    }

    private Quest Accept()
    {
        scenario.Accept(Owner, TextLinks);
        var quest = (Quest)scenario.QuestOf(Owner);
        Owner.Call(() => quest.StartQuest());
        return quest;
    }

    [Fact]
    public void Accept_CreatesWorldPartyOnlyOnServerAndPersonalQuestOnlyOnOwner()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Assert.Equal(1, creation.CountFor(Server));
        Assert.Equal(0, creation.CountFor(Owner));
        Assert.Equal(0, creation.CountFor(Other));
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.Null(notable.Issue.IssueQuest);
            Assert.Empty(notable.Issue.JournalEntries);
        });
    }

    [Fact]
    public void State_BindsExistingPartyAndProgressOnlyToOwner()
    {
        var quest = Accept();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var state = new NetworkExtortionByDesertersState(scenario.NotableId, QuestScenario.IssueId,
            QuestScenario.ControllerA, partyId, "", (int)State.DesertersAreDefeated, "deserters defeated", false);
        Owner.SimulateMessage(this, state);
        Other.SimulateMessage(this, state);
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.Same(party, quest._deserterMobileParty);
            Assert.Equal(State.DesertersAreDefeated, quest._currentState);
            Assert.Contains(quest.JournalEntries, log => log.LogText.ToString() == "deserters defeated");
        });
    }

    [Theory]
    [InlineData(1, 1f)]
    [InlineData(2, 0.5f)]
    [InlineData(3, 0f)]
    public void RewardChoice_RequestsItsBranchAndServerCompletesIt(byte branch, float multiplier)
    {
        var quest = Accept();
        var mirror = (Quest)scenario.QuestOf(Server);
        Server.Call(() => mirror._currentState = State.DesertersAreDefeated);
        using var result = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.ApplyQuestResult)));
        Owner.Call(() =>
        {
            var reward = multiplier == 1f ? quest._questResultSuccess1 :
                multiplier == 0.5f ? quest._questResultSuccess2 : quest._questResultSuccess3;
            quest.ApplyQuestResult(reward);
        });
        Assert.Equal(branch, Assert.Single(Owner.NetworkSentMessages.GetMessages<RequestQuestBranch>()).Proof);
        Assert.Equal(1, result.CountFor(Server));
        Assert.Equal(0, result.CountFor(Owner));
        Assert.Equal(IssueOutcome.QuestSuccess, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.True(quest.IsFinalized);
    }

    [Fact]
    public void RewardChoice_BeforeVictoryHasNoRewardOrCompletion()
    {
        var quest = Accept();
        using var result = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.ApplyQuestResult)));
        Owner.Call(() => quest.ApplyQuestResult(quest._questResultSuccess1));
        Assert.Equal(0, result.CountFor(Server));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.True(quest.IsOngoing);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void UnknownBranch_IsRejected(byte proof)
    {
        Accept();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IQuestTypeRegistry>().TryGet(typeof(Issue), out var descriptor));
            Assert.False(descriptor.FinalizationProofStrategy.TryRunBranch(scenario.QuestOf(Server), proof));
        });
    }

    [Fact]
    public void RaidReplayOnOwner_DoesNotApplyConsequences()
    {
        var quest = Accept();
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Village>(villageId, out var village));
            using (new AllowedThread()) quest.OnVillageBeingRaided(village);
            Assert.True(quest.IsOngoing);
        });
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }
}
