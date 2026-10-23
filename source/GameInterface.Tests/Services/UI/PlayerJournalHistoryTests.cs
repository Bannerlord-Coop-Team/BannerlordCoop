using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications;
using GameInterface.Tests.Services.SiegeEvents;
using Common.Util;
using Moq;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>
/// Checks persisted journal ownership independently of a running campaign and its active issue registry.
/// </summary>
[Collection(nameof(CampaignCurrentCollection))]
public class PlayerJournalHistoryTests
{
    [Theory]
    [InlineData("player-A", "player-A", true)]
    [InlineData("player-A", "player-B", false)]
    [InlineData(null, "player-B", false)]
    [InlineData(null, null, false)]
    public void SavedArchiveIsVisibleOnlyToItsController(string owner, string local, bool visible)
    {
        var controller = new Mock<IControllerIdProvider>();
        controller.SetupGet(value => value.ControllerId).Returns(local);
        var resolver = new QuestJournalOwnerResolver(Mock.Of<IPlayerManager>(), controller.Object);
        var entry = Entry(owner);

        Assert.Equal(visible, resolver.IsVisible(entry));
    }

    [Fact]
    public void CampaignSaveRoundTripRetainsOwnerEndingAndJournalText()
    {
        var original = Entry("player-A");
        original.Id = 42;
        original.GameTime = CampaignTime.Days(12);
        var history = new LogEntryHistory();
        var legacy = new JournalLogEntry(new TextObject("Legacy quest"), null, null, false);
        legacy.Update(new MBList<JournalLog>(), QuestBase.QuestCompleteDetails.Fail);
        history._logs.Add(legacy);
        history._logs.Add(original);
        history._logs.Add(Entry("player-B"));
        var metadata = new MetaData { ["ApplicationVersion"] = "v1.4.8.0" };
        var driver = new InMemDriver();
        var saved = SaveManager.Save(history, metadata, "player_journal", driver);
        Assert.True(saved.Successful, string.Join("; ", saved.Errors?.Select(error => error.ToString()) ?? Enumerable.Empty<string>()));

        var loaded = SaveManager.Load("player_journal", driver);
        Assert.True(loaded.Successful);
        var restoredHistory = Assert.IsType<LogEntryHistory>(loaded.Root);
        Assert.Equal(3, restoredHistory.GameActionLogs.Count);
        var restoredLegacy = Assert.IsType<JournalLogEntry>(restoredHistory.GameActionLogs[0]);
        Assert.Equal("Legacy quest", restoredLegacy.Title.ToString());
        Assert.True(restoredLegacy.IsEndedUnsuccessfully());
        var restored = Assert.IsType<PlayerJournalLogEntry>(restoredHistory.GameActionLogs[1]);
        Assert.Equal("player-B", Assert.IsType<PlayerJournalLogEntry>(restoredHistory.GameActionLogs[2]).OwnerControllerId);
        Assert.Equal("player-A", restored.OwnerControllerId);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.GameTime, restored.GameTime);
        Assert.Equal("Finished quest", restored.Title.ToString());
        Assert.Equal("quest_A", Assert.Single(restored._relatedObjectIds));
        Assert.True(restored.IsEnded());
        Assert.False(restored.IsEndedUnsuccessfully());
        Assert.Equal("Delivered the goods", Assert.Single(restored.GetEntries()).LogText.ToString());
    }

    private static PlayerJournalLogEntry Entry(string owner)
    {
        PlayerJournalLogEntry entry;
        using (new AllowedThread())
            entry = new PlayerJournalLogEntry(new JournalLogEntry(new TextObject("Finished quest"), null, null, false),
                owner, new MBObjectBase("quest_A"));
        Assert.Equal("quest_A", Assert.Single(entry._relatedObjectIds));
        entry.Update(new MBList<JournalLog>
        {
            new JournalLog(CampaignTime.Zero, new TextObject("Delivered the goods")),
        }, QuestBase.QuestCompleteDetails.Success);
        return entry;
    }
}
