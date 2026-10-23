using GameInterface.Services.Issues.Quests.CaravanAmbush;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class CaravanAmbushStateTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void WireRoundTripKeepsPartyIdentityProgressAndPersonalJournal(bool saved, int warnings)
    {
        var sent = new NetworkCaravanAmbushState("giver", "issue", "owner", "caravan", "raiders",
            saved, warnings, new[] { "Follow the caravan", "The caravan survived" });
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
        Assert.Equal(sent.Journal, received.Journal);
    }
}
