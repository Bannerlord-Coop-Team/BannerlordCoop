using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.Issues.Quests.TheConquestOfSettlement;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ConquestQuestOwnershipTests : IDisposable
{
    public ConquestQuestOwnershipTests()
    {
        TheConquestOfSettlementService.ResetQuestOwners();
    }

    public void Dispose()
    {
        TheConquestOfSettlementService.ResetQuestOwners();
    }

    [Theory]
    [InlineData("player-A", true)]
    [InlineData("player-B", false)]
    public void QuestVisibilityKeepsItsOwnerAfterIssueCleanupAndSaveRestore(string localController, bool visible)
    {
        var quest = ObjectHelper.SkipConstructor<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>();
        quest.StringId = "coop_conquest_issue_31_quest";
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(value => value.ControllerId).Returns(localController);
        var service = new TheConquestOfSettlementService(
            Mock.Of<IObjectManager>(),
            new IssueOwnershipRegistry(),
            Mock.Of<IPlayerManager>(),
            controller.Object);
        if (visible)
        {
            service.RecordLocalQuest(quest);
        }

        Assert.Equal(visible, service.IsVisible(quest));
        Assert.Equal(visible, service.IsVisible(quest));
    }

    [Fact]
    public void CompletedQuestVisibilitySurvivesSaveLoadWithFreshServices()
    {
        var quest = ObjectHelper.SkipConstructor<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>();
        quest.StringId = "coop_conquest_issue_31_quest";
        var records = new Dictionary<string, object>();
        var ownerController = new Mock<IControllerIdProvider>();
        ownerController.SetupGet(value => value.ControllerId).Returns("player-A");
        var observerController = new Mock<IControllerIdProvider>();
        observerController.SetupGet(value => value.ControllerId).Returns("player-B");

        var originalService = new TheConquestOfSettlementService(
            Mock.Of<IObjectManager>(),
            new IssueOwnershipRegistry(),
            Mock.Of<IPlayerManager>(),
            ownerController.Object);
        originalService.RecordLocalQuest(quest);
        TheConquestOfSettlementService.SyncQuestOwners(new TestDataStore(isSaving: true, records));

        TheConquestOfSettlementService.ResetQuestOwners();
        TheConquestOfSettlementService.SyncQuestOwners(new TestDataStore(isSaving: false, records));

        var restoredOwnerService = new TheConquestOfSettlementService(
            Mock.Of<IObjectManager>(),
            new IssueOwnershipRegistry(),
            Mock.Of<IPlayerManager>(),
            ownerController.Object);
        var observerService = new TheConquestOfSettlementService(
            Mock.Of<IObjectManager>(),
            new IssueOwnershipRegistry(),
            Mock.Of<IPlayerManager>(),
            observerController.Object);
        var entry = new JournalLogEntry(
            new TextObject(string.Empty),
            null,
            null,
            isSpecial: false,
            new MBObjectBase(quest.StringId));

        Assert.True(restoredOwnerService.IsVisible(quest));
        Assert.False(observerService.IsVisible(quest));
        Assert.True(restoredOwnerService.IsVisible(entry));
        Assert.False(observerService.IsVisible(entry));
    }

    [Fact]
    public void MissingIssueDoesNotSubstituteAnotherParty()
    {
        var ownership = new IssueOwnershipRegistry();
        var players = new Mock<IPlayerManager>();
        var objects = new Mock<IObjectManager>();
        var service = new TheConquestOfSettlementService(
            objects.Object,
            ownership,
            players.Object,
            Mock.Of<IControllerIdProvider>());

        Assert.False(service.TryOpenOwnerScope(null, out var scope));
        Assert.Null(scope);
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;

        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        public TestDataStore(bool isSaving, Dictionary<string, object> records)
        {
            IsSaving = isSaving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving)
            {
                records[key] = data;
                return true;
            }

            if (!records.TryGetValue(key, out var value)) return false;
            data = (T)value;
            return true;
        }
    }
}
