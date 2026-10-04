using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;
using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

[Collection(ModInformationRoleCollection.Name)]
public sealed class HeadmanHerdPersonalOwnershipTests : IDisposable
{
    private readonly bool originalRole = ModInformation.IsServer;
    private readonly ControllerIdProvider controller = new();
    private readonly HeadmanHerdPersonalOwnership ownership;
    private readonly IContainer container;

    public HeadmanHerdPersonalOwnershipTests()
    {
        ModInformation.IsServer = false;
        controller.SetControllerId("A");
        ownership = new HeadmanHerdPersonalOwnership(controller, Mock.Of<IPlayerManager>(), Mock.Of<IObjectManager>());
        var policy = new Mock<ISyncPolicy>();
        policy.Setup(x => x.AllowOriginal()).Returns(false);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(ownership).As<IHeadmanHerdPersonalOwnership>();
        builder.RegisterInstance(policy.Object).As<ISyncPolicy>();
        container = builder.Build();
        ContainerProvider.SetContainer(container);
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
        container.Dispose();
        ModInformation.IsServer = originalRole;
    }

    [Fact]
    public void JournalFiltersDirectAlternativeAndHistoryWithoutChangingCampaignCollections()
    {
        var owned = QuestWithId("quest-A");
        var other = QuestWithId("quest-B");
        var issueA = IssueWithId("issue-A");
        var issueB = IssueWithId("issue-B");
        ownership.SetOwner(owned.StringId, "A");
        ownership.SetOwner(other.StringId, "B");
        ownership.SetOwner(issueA.StringId, "A");
        ownership.SetOwner(issueB.StringId, "B");
        var quests = new QuestBase[] { owned, other };
        var issues = new[] { new KeyValuePair<Hero, IssueBase>(null, issueA), new KeyValuePair<Hero, IssueBase>(null, issueB) };
        var history = new[] { HistoryFor("issue-A"), HistoryFor("quest-B"), HistoryFor("unrelated") };

        Assert.Same(owned, Assert.Single(HeadmanHerdJournalVisibilityPatch.FilterQuests(quests)));
        Assert.Same(issueA, Assert.Single(HeadmanHerdJournalVisibilityPatch.FilterIssues(issues)).Value);
        Assert.Equal(new[] { history[0], history[2] }, HeadmanHerdJournalVisibilityPatch.FilterHistory(history));
        Assert.Equal(2, quests.Length);
        Assert.Equal(2, issues.Length);
        Assert.Equal(3, history.Length);
        ModInformation.IsServer = true;
        Assert.Same(quests, HeadmanHerdJournalVisibilityPatch.FilterQuests(quests));
    }

    [Fact]
    public void SavedOwnershipSurvivesCompletionAndReloadForAnotherLocalPlayer()
    {
        ownership.SetOwner("issue-A", "A");
        ownership.SetOwner("quest-A", "A");
        ownership.SetOwner("quest-B", "B");
        var data = new Dictionary<string, object>();
        HeadmanHerdPersonalOwnershipPersistencePatch.Postfix(new Store(true, data));
        ownership.Restore(Array.Empty<KeyValuePair<string, string>>());
        ownership.SetOwner("stale-campaign", "A");
        controller.SetControllerId("B");

        HeadmanHerdPersonalOwnershipPersistencePatch.Postfix(new Store(false, data));

        Assert.True(ownership.IsVisible(QuestWithId("quest-B")));
        Assert.False(ownership.IsVisible(QuestWithId("quest-A")));
        Assert.False(ownership.IsVisible(HistoryFor("issue-A", "quest-A")));
        Assert.DoesNotContain(ownership.Snapshot(), entry => entry.Key == "stale-campaign");
    }

    [Fact]
    public void AnotherPlayersRepeatedAcceptanceCannotReassignAQuestIdentity()
    {
        ownership.SetOwner("quest-A", "A");
        ownership.SetOwner("quest-A", "A");
        Assert.Throws<InvalidOperationException>(() => ownership.SetOwner("quest-A", "B"));
        Assert.True(ownership.IsLocalOwner("quest-A"));
    }

    [Fact]
    public void MissingPersonalOwnershipHidesQuestAndDisablesItsDialogue()
    {
        var quest = QuestWithId("unknown");
        Assert.False(ownership.IsVisible(quest));
        var result = true;
        Assert.False(HeadmanHerdDialogueOwnershipPatches.Prefix(quest, ref result));
        Assert.False(result);
        ownership.SetOwner("unknown", "A");
        Assert.True(HeadmanHerdDialogueOwnershipPatches.Prefix(quest, ref result));
    }

    [Fact]
    public void NotificationsBelongOnlyToLocalQuestOwnerEvenOnReceivedApply()
    {
        var owned = QuestWithId("quest-A");
        var other = IssueWithId("issue-B");
        ownership.SetOwner(owned.StringId, "A");
        ownership.SetOwner(other.StringId, "B");
        using (new AllowedThread())
        {
            Assert.True(HeadmanHerdNotificationPatches.Prefix(owned));
            Assert.False(HeadmanHerdNotificationPatches.Prefix(other));
            ModInformation.IsServer = true;
            Assert.False(HeadmanHerdNotificationPatches.Prefix(owned));
        }
    }

    private static Quest QuestWithId(string id)
    {
        var quest = ObjectHelper.SkipConstructor<Quest>();
        quest.StringId = id;
        return quest;
    }

    private static Issue IssueWithId(string id)
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue.StringId = id;
        return issue;
    }

    private static JournalLogEntry HistoryFor(params string[] ids)
    {
        var history = ObjectHelper.SkipConstructor<JournalLogEntry>();
        // Publicizer preserves this readonly identity array.
        AccessTools.Field(typeof(JournalLogEntry), nameof(history._relatedObjectIds)).SetValue(history, ids);
        return history;
    }

    private sealed class Store : IDataStore
    {
        private readonly Dictionary<string, object> data;
        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;
        public Store(bool saving, Dictionary<string, object> data) { IsSaving = saving; this.data = data; }
        public bool SyncData<T>(string key, ref T value)
        {
            if (IsSaving) data[key] = value;
            else value = data.TryGetValue(key, out var saved) ? (T)saved : default;
            return true;
        }
    }
}
