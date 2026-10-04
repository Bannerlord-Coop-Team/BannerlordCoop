using Autofac;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Surrogates;
using HarmonyLib;
using Moq;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public class ArtisanJournalTests : IDisposable
{
    public void Dispose() => ContainerProvider.Clear();

    [Fact]
    public void ArchivedJournalKeepsItsOwnerAfterIssueOwnershipIsCleared()
    {
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("player-one");
        var registry = new IssueOwnershipRegistry();
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue.StringId = "issue_813";
        var subject = new ArtisanJournalOwnership(registry, controller.Object,
            Mock.Of<IPlayerManager>(), Mock.Of<IObjectManager>());
        registry.SetOwner(giver, "player-one");
        subject.Record(issue, giver);
        registry.Clear(giver);
        var log = ObjectHelper.SkipConstructor<JournalLogEntry>();
        AccessTools.Field(typeof(JournalLogEntry), "_relatedObjectIds").SetValue(log, new[] { "issue_813" });

        Assert.True(subject.IsVisible(log));
        controller.SetupGet(x => x.ControllerId).Returns("player-two");
        Assert.False(subject.IsVisible(log));
    }

    [Fact]
    public void SavedArchiveOwnershipLoadsIntoANewServiceWithoutReattributingUnknownLogs()
    {
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(x => x.ControllerId).Returns("player-one");
        var registry = new IssueOwnershipRegistry();
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue.StringId = "issue_813";
        var subject = new ArtisanJournalOwnership(registry, controller.Object,
            Mock.Of<IPlayerManager>(), Mock.Of<IObjectManager>());
        registry.SetOwner(giver, "player-one");
        subject.Record(issue, giver);
        registry.Clear(giver);
        var unknown = ObjectHelper.SkipConstructor<Issue>();
        unknown.StringId = "issue_814";
        subject.Record(unknown, giver);
        var records = new Dictionary<string, object>();
        subject.SyncData(new JournalDataStore(true, records));
        var restored = new ArtisanJournalOwnership(registry, controller.Object,
            Mock.Of<IPlayerManager>(), Mock.Of<IObjectManager>());
        restored.SyncData(new JournalDataStore(false, records));
        var ownedLog = ObjectHelper.SkipConstructor<JournalLogEntry>();
        var unknownLog = ObjectHelper.SkipConstructor<JournalLogEntry>();
        AccessTools.Field(typeof(JournalLogEntry), "_relatedObjectIds").SetValue(ownedLog, new[] { issue.StringId });
        AccessTools.Field(typeof(JournalLogEntry), "_relatedObjectIds").SetValue(unknownLog, new[] { unknown.StringId });
        Assert.True(restored.IsVisible(ownedLog));
        Assert.False(restored.IsVisible(unknownLog));
        controller.SetupGet(x => x.ControllerId).Returns("player-two");
        Assert.False(restored.IsVisible(ownedLog));
        Assert.False(restored.IsVisible(unknownLog));
    }

    private sealed class JournalDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;
        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        public JournalDataStore(bool saving, Dictionary<string, object> records)
        {
            IsSaving = saving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving) records[key] = data;
            else if (records.TryGetValue(key, out var value)) data = (T)value;
            else return false;
            return true;
        }
    }

    [Theory]
    [InlineData(IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest)]
    [InlineData(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest)]
    [InlineData(IssueBase.IssueUpdateDetails.IssueCancel)]
    [InlineData(IssueBase.IssueUpdateDetails.IssueFinishedWithBetrayal)]
    public void OutcomeWireRetainsActualStatusAndJournalTask(IssueBase.IssueUpdateDetails details)
    {
        var model = RuntimeTypeModel.Create();
        model.Add(typeof(CampaignTime), false).SetSurrogate(typeof(CampaignTimeSurrogate));
        model.Add(typeof(TextObject), false).SetSurrogate(typeof(TextObjectSurrogate));
        var log = new JournalLog(new CampaignTime(728), new TextObject("{=!}companion returned"),
            new TextObject("{=!}Return Days"), 3, 7, LogType.Text);
        var original = new NetworkArtisanIssueOutcome("artisan", 9, "player-one", details,
            new[] { new ArtisanJournalEntry(log) }, true);
        using var stream = new MemoryStream();
        model.Serialize(stream, original);
        stream.Position = 0;
        var copy = (NetworkArtisanIssueOutcome)model.Deserialize(stream, null, original.GetType());

        Assert.Equal(details, copy.Details);
        Assert.Equal(9, copy.Generation);
        Assert.Equal("player-one", copy.ControllerId);
        Assert.True(copy.EffectsResolved);
        var restored = Assert.Single(copy.Entries).ToLog();
        Assert.Equal(log.LogTime, restored.LogTime);
        Assert.Equal(log.LogText.ToString(), restored.LogText.ToString());
        Assert.Equal(log.TaskName.ToString(), restored.TaskName.ToString());
        Assert.Equal(3, restored.CurrentProgress);
        Assert.Equal(7, restored.Range);
    }

    [Fact]
    public void DiscussionRequiresOwnershipAndPreservesVanillaCondition()
    {
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(x => x.AllowOriginal()).Returns(false);
        var context = new Mock<IArtisanQuestOwnerContext>();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(policy.Object).As<ISyncPolicy>();
        builder.RegisterInstance(context.Object).As<IArtisanQuestOwnerContext>();
        ContainerProvider.SetContainer(builder.Build());
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest.DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss");
        var allowedByVanilla = true;
        var line = new DialogFlowLine { InputToken = "quest_discuss", ConditionDelegate = () => allowedByVanilla };
        quest.DiscussDialogFlow.Lines.Add(line);
        ArtisanQuestDiscussionPatch.Postfix(quest);

        Assert.False(line.ConditionDelegate());
        context.Setup(x => x.IsLocalOwner(quest.QuestGiver)).Returns(true);
        Assert.True(line.ConditionDelegate());
        allowedByVanilla = false;
        Assert.False(line.ConditionDelegate());
    }
}
