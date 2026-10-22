using GameInterface.Services.Entity;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications;
using GameInterface.Tests.Services.SiegeEvents;
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
        var metadata = new MetaData { ["ApplicationVersion"] = "v1.4.8.0" };
        var driver = new InMemDriver();
        var saved = SaveManager.Save(original, metadata, "player_journal", driver);
        Assert.True(saved.Successful, string.Join("; ", saved.Errors?.Select(error => error.ToString()) ?? Enumerable.Empty<string>()));

        var loaded = SaveManager.Load("player_journal", driver);
        Assert.True(loaded.Successful);
        var restored = Assert.IsType<PlayerJournalLogEntry>(loaded.Root);
        Assert.Equal("player-A", restored.OwnerControllerId);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.GameTime, restored.GameTime);
        Assert.Equal("Finished quest", restored.Title.ToString());
        Assert.True(restored.IsRelatedTo(new MBObjectBase("quest_A")));
        Assert.True(restored.IsEnded());
        Assert.False(restored.IsEndedUnsuccessfully());
        Assert.Equal("Delivered the goods", Assert.Single(restored.GetEntries()).LogText.ToString());
    }

    private static PlayerJournalLogEntry Entry(string owner)
    {
        var entry = new PlayerJournalLogEntry(new JournalLogEntry(new TextObject("Finished quest"), null, null, false),
            owner, new MBObjectBase("quest_A"));
        entry.Update(new MBList<JournalLog>
        {
            new JournalLog(CampaignTime.Zero, new TextObject("Delivered the goods")),
        }, QuestBase.QuestCompleteDetails.Success);
        return entry;
    }
}
