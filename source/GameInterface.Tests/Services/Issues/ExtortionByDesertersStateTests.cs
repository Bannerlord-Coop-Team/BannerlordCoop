using Common.Serialization;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Handlers;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Surrogates;
using System.Collections.Generic;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

/// <summary>Preserves quest generation, ownership, parties and progress across the wire.</summary>
public class ExtortionByDesertersStateTests
{
    private readonly ProtoBufSerializer serializer = new(new SerializableTypeMapper());

    public ExtortionByDesertersStateTests() => new SurrogateCollection();

    [Theory]
    [InlineData(0, "", "", false)]
    [InlineData(1, "deserters", "", false)]
    [InlineData(2, "deserters", "defenders", true)]
    [InlineData(3, "", "", false)]
    public void RoundTrip_PreservesOwnerAndProgress(int state, string deserters, string defenders, bool ambush)
    {
        var sent = new NetworkExtortionByDesertersState("headman", "issue-generation", "player-A",
            deserters, defenders, state, new TextObject("{=!}quest progress"), ambush, 3);
        var received = RoundTrip(sent);
        Assert.Equal("headman", received.IssueOwnerId);
        Assert.Equal("issue-generation", received.IssueId);
        Assert.Equal("player-A", received.ControllerId);
        Assert.Equal(deserters, received.DeserterId);
        Assert.Equal(defenders, received.DefenderId);
        Assert.Equal(state, received.State);
        Assert.Equal("{=!}quest progress", received.Log.Value);
        Assert.Equal(ambush, received.Ambush);
        Assert.Equal(3, received.Notice);
    }

    [Fact]
    public void RoundTrip_PreservesLocalizationAndNestedAttributesInBothJournalPaths()
    {
        var link = new TextObject("{=!}<a href=\"{ID}\">{NAME}</a>", new Dictionary<string, object>
        {
            ["ID"] = "headman",
            ["NAME"] = new TextObject("{=!}Danustica headman"),
        });
        var properties = TextObject.GetEmpty();
        properties.SetTextVariable("NAME", new TextObject("{=!}Danustica headman"));
        properties.SetTextVariable("GENDER", 0);
        properties.SetTextVariable("LINK", link);
        var text = new TextObject("{=YjxCkglX}You've failed to complete this quest in time. Your agreement with {QUEST_GIVER.LINK} was canceled.");
        text.SetTextVariable("QUEST_GIVER", properties);
        var state = RoundTrip(new NetworkExtortionByDesertersState("headman", "issue-generation", "player-B",
            "", "", 0, text, false));
        var snapshot = RoundTrip(new NetworkIssueJournal("headman", "issue-generation", true, new[] { text }));

        foreach (var log in new[] { state.Log, Assert.Single(snapshot.Logs) })
        {
            Assert.Equal(text.Value, log.Value);
            var giver = Assert.IsType<TextObject>(log.Attributes["QUEST_GIVER"]);
            Assert.Equal("{=!}Danustica headman", Assert.IsType<TextObject>(giver.Attributes["NAME"]).Value);
            Assert.Equal(0, Assert.IsType<int>(giver.Attributes["GENDER"]));
            var receivedLink = Assert.IsType<TextObject>(giver.Attributes["LINK"]);
            Assert.Equal(link.Value, receivedLink.Value);
            Assert.Equal("headman", Assert.IsType<string>(receivedLink.Attributes["ID"]));
            Assert.Equal("{=!}Danustica headman", Assert.IsType<TextObject>(receivedLink.Attributes["NAME"]).Value);
        }
    }

    [Fact]
    public void RoundTrip_NullAttributeHasTheSameRepresentationInBothJournalPaths()
    {
        var text = new TextObject("{=YjxCkglX}You've failed to complete this quest in time. Your agreement with {QUEST_GIVER.LINK} was canceled.",
            new Dictionary<string, object> { ["QUEST_GIVER"] = null });
        var state = RoundTrip(new NetworkExtortionByDesertersState("headman", "issue-generation", "player-B",
            "", "", 0, text, false));
        var snapshot = RoundTrip(new NetworkIssueJournal("headman", "issue-generation", true, new[] { text }));
        var ending = Assert.Single(snapshot.Logs);

        Assert.Equal(text.Value, state.Log.Value);
        Assert.Equal(ending.Value, state.Log.Value);
        Assert.False(state.Log.Attributes?.ContainsKey("QUEST_GIVER") ?? false);
        Assert.False(ending.Attributes?.ContainsKey("QUEST_GIVER") ?? false);
    }

    [Fact]
    public void RoundTrip_StateOnlyAndNoticeOnlyKeepNullLog()
    {
        foreach (byte notice in new byte[] { 0, 1, 2, 3 })
        {
            var state = RoundTrip(new NetworkExtortionByDesertersState("headman", "issue-generation", "player-B",
                "deserters", "", 1, null, false, notice));
            Assert.Null(state.Log);
            Assert.Equal(notice, state.Notice);
        }
    }

    private T RoundTrip<T>(T message) => serializer.Deserialize<T>(serializer.Serialize(message));
}
