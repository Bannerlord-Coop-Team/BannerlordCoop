using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Patches;
using Moq;
using TaleWorlds.CampaignSystem.LogEntries;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ExtortionQuestJournalTests
{
    [Fact]
    public void CompletedQuestRemainsPersonalAfterActiveOwnershipIsCleared()
    {
        var ownership = new IssueOwnershipRegistry();
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(c => c.ControllerId).Returns("player-one");
        var journal = new ExtortionQuestJournal(ownership, controller.Object);
        journal.Restore(new[] { new IssueOwnershipSaveData(null, "player-one", "old-issue") });
        var relatedIds = new[] { "old-issue", "old-quest" };

        ownership.ClearAll();
        Assert.True(journal.IsVisible(relatedIds));
        controller.SetupGet(c => c.ControllerId).Returns("player-two");
        Assert.False(journal.IsVisible(relatedIds));
        Assert.True(journal.IsVisible(new[] { "unrelated-quest" }));
    }
}
