using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Surrogates;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Quest = ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;
using QuestSettlement = ScoutEnemyGarrisonsIssueBehavior.QuestSettlement;

[Collection(ModInformationRoleCollection.Name)]
public class ScoutEnemyGarrisonsTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;
    private readonly ScoutEnemyGarrisonsQuestState state;

    public ScoutEnemyGarrisonsTests()
    {
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("player-A");
        state = new ScoutEnemyGarrisonsQuestState(controller.Object);
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(x => x.AllowOriginal()).Returns(false);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(state).As<IScoutEnemyGarrisonsQuestState>();
        builder.RegisterInstance(policy.Object).As<ISyncPolicy>();
        ContainerProvider.SetContainer(builder.Build());
        ModInformation.IsServer = false;
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void VisibilityUsesRecordedControllerEvenWhenApplyingAnotherPlayersMessage()
    {
        var owned = NewQuest("issue_1_quest");
        var other = NewQuest("issue_2_quest");
        Remember(owned, "player-A");
        Remember(other, "player-B");

        using (new AllowedThread())
        {
            Assert.True(state.IsVisible(owned));
            Assert.False(state.IsVisible(other));
            Assert.False(state.IsVisible(NewQuest("unknown")));
            Assert.True(ScoutEnemyGarrisonsNotificationPatch.Prefix(owned));
            Assert.False(ScoutEnemyGarrisonsNotificationPatch.Prefix(other));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void SavedOwnershipRestoresNeutralTargetsWithoutTreatingScoutedTargetsAsNeutral(int neutralTargets)
    {
        var quest = NewQuest("issue_3_quest");
        Remember(quest, "player-A");
        quest._questSettlement1.IsCompletedThroughBeingNeutral = (neutralTargets & 1) != 0;
        quest._questSettlement2.IsCompletedThroughBeingNeutral = (neutralTargets & 2) != 0;
        quest._questSettlement3.IsCompletedThroughBeingNeutral = (neutralTargets & 4) != 0;
        state.Capture(quest);

        List<ScoutEnemyGarrisonsQuestOwner> saved = null!;
        var saving = new Mock<IDataStore>();
        saving.SetupGet(x => x.IsSaving).Returns(true);
        saving.Setup(x => x.SyncData(It.IsAny<string>(), ref It.Ref<List<ScoutEnemyGarrisonsQuestOwner>>.IsAny))
            .Callback(new SyncOwners((string _, ref List<ScoutEnemyGarrisonsQuestOwner> value) => saved = value));
        state.SyncData(saving.Object);

        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("player-A");
        var restored = new ScoutEnemyGarrisonsQuestState(controller.Object);
        var loading = new Mock<IDataStore>();
        loading.SetupGet(x => x.IsLoading).Returns(true);
        loading.Setup(x => x.SyncData(It.IsAny<string>(), ref It.Ref<List<ScoutEnemyGarrisonsQuestOwner>>.IsAny))
            .Callback(new SyncOwners((string _, ref List<ScoutEnemyGarrisonsQuestOwner> value) => value = saved));
        restored.SyncData(loading.Object);
        var loadedQuest = NewQuest(quest.StringId);
        restored.Restore(loadedQuest);

        Assert.True(restored.IsVisible(loadedQuest));
        Assert.Equal((neutralTargets & 1) != 0, loadedQuest._questSettlement1.IsCompletedThroughBeingNeutral);
        Assert.Equal((neutralTargets & 2) != 0, loadedQuest._questSettlement2.IsCompletedThroughBeingNeutral);
        Assert.Equal((neutralTargets & 4) != 0, loadedQuest._questSettlement3.IsCompletedThroughBeingNeutral);
        Assert.Equal(8, loadedQuest._questSettlement1.CurrentScoutProgress);
        Assert.True(restored.TryGet(loadedQuest, out var owner));
        Assert.Same(saved[0].Hero, owner.Hero);
        Assert.Same(saved[0].Party, owner.Party);
    }

    [Fact]
    public void ClientWorldEventCannotAdvanceScoutingInsideAllowedThread()
    {
        using (new AllowedThread())
        {
            Assert.False(ScoutEnemyGarrisonsAuthorityPatches.Prefix(NewQuest("issue_4_quest"), out var scope));
            Assert.Null(scope);
        }
    }

    [Fact]
    public void ClientCompletionRequiresBothReceivedApplicationAndFinalizationAuthority()
    {
        var quest = NewQuest("issue_5_quest");
        using (new AllowedThread())
        {
            Assert.False(ScoutEnemyGarrisonsCompletionPatches.Prefix(quest, null!, out _));
            using (new IssueFinalizeAuthorityGuard())
                Assert.True(ScoutEnemyGarrisonsCompletionPatches.Prefix(quest, null!, out _));
        }
        using (new IssueFinalizeAuthorityGuard())
            Assert.False(ScoutEnemyGarrisonsCompletionPatches.Prefix(quest, null!, out _));
    }

    [Fact]
    public void AcceptPayloadRetainsAuthoritativeIssueIdentityTargetsAndDeadline()
    {
        _ = new SurrogateCollection();
        var targets = new[] { "target-1", "target-2", "target-3" };
        var data = new ScoutEnemyGarrisonsAccept("player-A", "hero-A", "party-A", targets, new CampaignTime(12345), "issue_6");

        var received = GenericAcceptFieldsSerializer.Deserialize<ScoutEnemyGarrisonsAccept>(GenericAcceptFieldsSerializer.Serialize(data));

        Assert.Equal("issue_6", received.IssueId);
        Assert.Equal("player-A", received.ControllerId);
        Assert.Equal("hero-A", received.HeroId);
        Assert.Equal("party-A", received.PartyId);
        Assert.Equal(targets, received.TargetIds);
        Assert.Equal(12345, received.DueTime.NumTicks);
    }

    [Fact]
    public void ProgressPayloadRetainsLocalizedReportVariablesAndPartialCounters()
    {
        _ = new SurrogateCollection();
        var report = new TextObject("{=!}{SETTLEMENT}: {GARRISON_SIZE}");
        report.SetTextVariable("SETTLEMENT", new TextObject("{=!}Danustica"));
        report.SetTextVariable("GARRISON_SIZE", 137);
        var log = new JournalLog(new CampaignTime(456), report, new TextObject("{=!}Settlements"), 1, 3, LogType.Discreate);
        var data = new NetworkScoutEnemyGarrisonsProgress("giver", 4, "issue_7_quest", new[] { 8, 4, 0 }, 1, 1,
            new[] { new ScoutEnemyGarrisonsLog(log) }, 0);

        var received = GenericAcceptFieldsSerializer.Deserialize<NetworkScoutEnemyGarrisonsProgress>(GenericAcceptFieldsSerializer.Serialize(data));

        Assert.Equal(4, received.Generation);
        Assert.Equal("issue_7_quest", received.QuestId);
        Assert.Equal(new[] { 8, 4, 0 }, received.Hours);
        Assert.Equal(1, received.NeutralTargets);
        Assert.Equal(1, received.ScoutedCount);
        Assert.Equal(137, received.Logs[0].Text.Attributes["GARRISON_SIZE"]);
        Assert.Equal("{=!}Danustica", ((TextObject)received.Logs[0].Text.Attributes["SETTLEMENT"]).Value);
        Assert.Equal(1, received.Logs[0].Progress);
    }

    private void Remember(Quest quest, string controller) => state.Remember(quest, controller,
        ObjectHelper.SkipConstructor<Hero>(), ObjectHelper.SkipConstructor<MobileParty>());

    private static Quest NewQuest(string id)
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest.StringId = id;
        quest._questSettlement1 = new QuestSettlement(null!, 8);
        quest._questSettlement2 = new QuestSettlement(null!, 8);
        quest._questSettlement3 = new QuestSettlement(null!, 8);
        return quest;
    }

    private delegate void SyncOwners(string key, ref List<ScoutEnemyGarrisonsQuestOwner> value);
}
