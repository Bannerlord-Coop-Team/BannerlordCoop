using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

/// <summary>Preserves quest generation, ownership, parties and progress across the wire.</summary>
public class ExtortionByDesertersStateTests
{
    [Theory]
    [InlineData(0, "", "", false)]
    [InlineData(1, "deserters", "", false)]
    [InlineData(2, "deserters", "defenders", true)]
    [InlineData(3, "", "", false)]
    public void RoundTrip_PreservesOwnerAndProgress(int state, string deserters, string defenders, bool ambush)
    {
        var sent = new NetworkExtortionByDesertersState("headman", "issue-generation", "player-A",
            deserters, defenders, state, "quest progress", ambush);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, sent);
        stream.Position = 0;
        var received = Serializer.Deserialize<NetworkExtortionByDesertersState>(stream);
        Assert.Equal("headman", received.IssueOwnerId);
        Assert.Equal("issue-generation", received.IssueId);
        Assert.Equal("player-A", received.ControllerId);
        Assert.Equal(deserters, received.DeserterId);
        Assert.Equal(defenders, received.DefenderId);
        Assert.Equal(state, received.State);
        Assert.Equal("quest progress", received.Log);
        Assert.Equal(ambush, received.Ambush);
    }
}
