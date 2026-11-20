using Common.Serialization;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using GameInterface.Surrogates;
using TaleWorlds.Localization;
using Xunit;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Tests.Services.Issues;

/// <summary>Preserves quest generation, ownership, parties and progress across the wire.</summary>
public class ExtortionByDesertersStateTests
{
    private readonly ProtoBufSerializer serializer = new(new SerializableTypeMapper());

    public ExtortionByDesertersStateTests() => new SurrogateCollection();

    [Fact]
    public void RoundTrip_PreservesOwnerAndProgress()
    {
        var sent = new NetworkExtortionByDesertersState("headman", "issue-generation", "player-A",
            "deserters", "defenders", (int)State.DesertersDefeatedPlayer,
            new TextObject("{=!}quest progress"), (byte)ExtortionByDesertersNotice.Ambush);
        var received = RoundTrip(sent);
        Assert.Equal("headman", received.IssueOwnerId);
        Assert.Equal("issue-generation", received.IssueId);
        Assert.Equal("player-A", received.ControllerId);
        Assert.Equal("deserters", received.DeserterId);
        Assert.Equal("defenders", received.DefenderId);
        Assert.Equal((int)State.DesertersDefeatedPlayer, received.State);
        Assert.Equal("{=!}quest progress", received.Log.Value);
        Assert.Equal((byte)ExtortionByDesertersNotice.Ambush, received.Notice);
    }

    private T RoundTrip<T>(T message) => serializer.Deserialize<T>(serializer.Serialize(message));
}
