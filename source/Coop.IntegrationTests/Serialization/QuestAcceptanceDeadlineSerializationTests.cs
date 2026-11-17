using GameInterface.Services.Issues.Framework.AcceptCoordination;
using ProtoBuf;

namespace Coop.IntegrationTests.Serialization;

/// <summary>Preserves the original acceptance fields and optional absolute deadline wire contract.</summary>
public class QuestAcceptanceDeadlineSerializationTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(78719763460921L)]
    [InlineData(long.MaxValue)]
    public void NewDeadlineSurvivesRoundTripAndOldReadersKeepTheOriginalFields(long deadline)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkQuestSolutionAccepted("giver", "issue", "owner", 1.25f,
            new byte[] { 1, 2 }, deadline, true));
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkQuestSolutionAccepted>(stream);
        Assert.True(copy.HasQuestDueTime);
        Assert.Equal(deadline, copy.QuestDueTimeTicks);

        stream.Position = 0;
        var legacy = Serializer.Deserialize<LegacyAccepted>(stream);
        Assert.Equal("giver", legacy.IssueOwnerId);
        Assert.Equal("issue", legacy.IssueId);
        Assert.Equal("owner", legacy.ControllerId);
        Assert.Equal(1.25f, legacy.DifficultyMultiplier);
        Assert.Equal(new byte[] { 1, 2 }, legacy.Captured);
    }

    [Fact]
    public void LiteralLegacyPayloadLeavesDeadlineAbsent()
    {
        using var stream = new MemoryStream(Convert.FromHexString(
            "0A056769766572120569737375651A056F776E6572250000A03F2A020102"));
        var copy = Serializer.Deserialize<NetworkQuestSolutionAccepted>(stream);
        Assert.Equal("giver", copy.IssueOwnerId);
        Assert.Equal("issue", copy.IssueId);
        Assert.Equal("owner", copy.ControllerId);
        Assert.Equal(1.25f, copy.DifficultyMultiplier);
        Assert.Equal(new byte[] { 1, 2 }, copy.Captured);
        Assert.False(copy.HasQuestDueTime);
        Assert.Equal(0, copy.QuestDueTimeTicks);
    }

    [ProtoContract]
    private sealed class LegacyAccepted
    {
        [ProtoMember(1)] public string IssueOwnerId { get; set; } = null!;
        [ProtoMember(2)] public string IssueId { get; set; } = null!;
        [ProtoMember(3)] public string ControllerId { get; set; } = null!;
        [ProtoMember(4)] public float DifficultyMultiplier { get; set; }
        [ProtoMember(5)] public byte[] Captured { get; set; } = null!;
    }
}
