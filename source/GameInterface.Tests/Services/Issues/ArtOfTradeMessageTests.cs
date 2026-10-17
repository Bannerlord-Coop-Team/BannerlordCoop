using GameInterface.Services.Issues.Quests.ArtOfTrade;
using GameInterface.Surrogates;
using ProtoBuf;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class ArtOfTradeMessageTests
{
    [Fact]
    public void ProgressRetainsTheIssueIdentityAndSaleTotals()
    {
        var copy = RoundTrip(new RequestArtOfTradeProgress("giver", "issue_7", 9, 120, 1));
        Assert.Equal("giver", copy.IssueOwnerId);
        Assert.Equal("issue_7", copy.IssueId);
        Assert.Equal(9, copy.SoldCount);
        Assert.Equal(120, copy.GatheredDenars);
        Assert.Equal(1, copy.DaysPassed);
    }

    [Fact]
    public void FinalJournalRetainsOwnerAndLocalizedProgress()
    {
        _ = new SurrogateCollection();
        var text = new TextObject("{=trade_test}{COUNT} {ITEM}");
        text.SetTextVariable("COUNT", 9);
        text.SetTextVariable("ITEM", new TextObject("{=grain}Grain"));
        var entry = new ArtOfTradeJournalEntry(new JournalLog(new CampaignTime(1234), text,
            new TextObject("Sales"), 9, 14, LogType.Discreate));

        var copy = RoundTrip(new NetworkArtOfTradeJournal("giver", "issue_7", "owner", new[] { entry }));
        var restored = Assert.Single(copy.Entries).CreateLog();

        Assert.Equal("giver", copy.IssueOwnerId);
        Assert.Equal("issue_7", copy.IssueId);
        Assert.Equal("owner", copy.ControllerId);
        Assert.Equal(new CampaignTime(1234), restored.LogTime);
        Assert.Equal(text.Value, restored.LogText.Value);
        Assert.Equal(9, restored.LogText.Attributes["COUNT"]);
        Assert.Equal("{=grain}Grain", Assert.IsType<TextObject>(restored.LogText.Attributes["ITEM"]).Value);
        Assert.Equal(9, restored.CurrentProgress);
        Assert.Equal(14, restored.Range);
        Assert.Equal(LogType.Discreate, restored.Type);
    }

    [Fact]
    public void CompanionJournalRetainsTheCompletionStatusAndResolvedEffects()
    {
        var copy = RoundTrip(new NetworkArtOfTradeJournal("giver", "issue_7", "owner",
            System.Array.Empty<ArtOfTradeJournalEntry>(), true, IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest, true));
        Assert.True(copy.IsIssueJournal);
        Assert.True(copy.EffectsResolved);
        Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest, copy.Details);
    }

    private static T RoundTrip<T>(T value)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, value);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }
}
