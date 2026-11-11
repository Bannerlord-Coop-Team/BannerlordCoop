using GameInterface.Services.Issues.Quests.CaravanAmbush;
using ProtoBuf;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Surrogates;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class CaravanAmbushStateTests
{
    public CaravanAmbushStateTests() => new SurrogateCollection();

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void WireRoundTripKeepsPartyIdentityProgressAndPersonalJournal(bool saved, int warnings)
    {
        var sent = new NetworkCaravanAmbushState("giver", "issue", "owner", "caravan", "raiders",
            saved, warnings, new[]
            {
                new IssueJournalEntry(new JournalLog(new CampaignTime(123456L),
                    new TextObject("{=caravan_follow_test}Follow the caravan"))),
                new IssueJournalEntry(new JournalLog(new CampaignTime(234567L),
                    new TextObject("{=caravan_saved_test}The caravan survived"),
                    new TextObject("{=caravan_count_test}Caravans saved"), 2, 5, LogType.Discreate))
            });
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, sent);
        stream.Position = 0;
        var received = Serializer.Deserialize<NetworkCaravanAmbushState>(stream);
        Assert.Equal("giver", received.IssueOwnerId);
        Assert.Equal("issue", received.IssueId);
        Assert.Equal("owner", received.ControllerId);
        Assert.Equal("caravan", received.CaravanId);
        Assert.Equal("raiders", received.BanditId);
        Assert.Equal(saved, received.Saved);
        Assert.Equal(warnings, received.Warnings);
        Assert.Equal(2, received.Journal.Length);
        for (var i = 0; i < sent.Journal.Length; i++)
        {
            Assert.Equal(sent.Journal[i].Text.Value, received.Journal[i].Text.Value);
            Assert.Equal(sent.Journal[i].Time.NumTicks, received.Journal[i].Time.NumTicks);
            Assert.Equal(sent.Journal[i].TaskName?.Value, received.Journal[i].TaskName?.Value);
            Assert.Equal(sent.Journal[i].Progress, received.Journal[i].Progress);
            Assert.Equal(sent.Journal[i].Range, received.Journal[i].Range);
            Assert.Equal(sent.Journal[i].Type, received.Journal[i].Type);
        }
    }
}
