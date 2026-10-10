using GameInterface.Services.Hideouts;
using GameInterface.Services.Hideouts.Messages;
using ProtoBuf;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class HideoutTroopSelectionSerializationTests
{
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 0)]
    public void EntryReply_RoundTripPreservesSharedCapacityAndExistingAdmission(bool isAdmitted, int remaining)
    {
        var original = new NetworkHideoutRaidEntryReply("request-1", true, "raid-1", remaining,
            false, true, isAdmitted: isAdmitted);
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var result = Serializer.Deserialize<NetworkHideoutRaidEntryReply>(stream);

        Assert.Equal(original.RequestId, result.RequestId);
        Assert.True(result.Accepted);
        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal(remaining, result.RemainingTroops);
        Assert.False(result.IsDirectAssault);
        Assert.True(result.IsJoining);
        Assert.Equal(isAdmitted, result.IsAdmitted);
    }

    [Fact]
    public void EntryRequest_RoundTripPreservesOrderedSelection()
    {
        var original = new NetworkHideoutRaidEntryRequest("request-1", "hideout-1", true, false,
            new[] { new HideoutTroopSelectionEntry("player-hero", 1),
                new HideoutTroopSelectionEntry("escort", 14) });
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var result = Serializer.Deserialize<NetworkHideoutRaidEntryRequest>(stream);
        Assert.Equal(original.RequestId, result.RequestId);
        Assert.Equal(original.SettlementId, result.SettlementId);
        Assert.True(result.IsDirectAssault);
        Assert.False(result.QueryOnly);
        Assert.Equal(original.Troops, result.Troops);
    }
}
