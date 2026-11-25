using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.SiegeEvents.Messages;
using ProtoBuf;
using System;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class SiegeKeepSerializationTests
{
    [Fact]
    public void EntryReply_RoundTripPreservesAllowanceAndLock()
    {
        var original = new NetworkSiegeKeepEntryReply("request-1", true, null, 6, 42, isLocked: true);

        var result = RoundTrip(original);

        Assert.Equal(original.RequestId, result.RequestId);
        Assert.True(result.Accepted);
        Assert.Equal(6, result.EscortAllowance);
        Assert.Equal(42, result.SecondsRemaining);
        Assert.True(result.IsLocked);
    }

    [Fact]
    public void PhaseChanged_RoundTripPreservesPhaseAndSeat()
    {
        var original = new NetworkSiegeKeepPhaseChanged("map-event-1", SiegeKeepPhase.Locked, true, 4, 73);

        var result = RoundTrip(original);

        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal(SiegeKeepPhase.Locked, result.Phase);
        Assert.True(result.IsSeated);
        Assert.Equal(4, result.EscortAllowance);
        Assert.Equal(73, result.SecondsRemaining);
    }

    [Fact]
    public void EntryRequest_RoundTripPreservesOrderedSelection()
    {
        var original = new NetworkSiegeKeepEntryRequest("request-1", "map-event-1", false,
            new[] { new HideoutTroopSelectionEntry("player-hero", 1), new HideoutTroopSelectionEntry("escort", 6) });

        var result = RoundTrip(original);

        Assert.Equal(original.RequestId, result.RequestId);
        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.False(result.QueryOnly);
        Assert.Equal(original.Troops, result.Troops);
    }

    // protobuf-net writes nothing for an empty struct array, so a query arrives with no troops at all.
    [Fact]
    public void EntryRequest_EmptySelectionArrivesAsNull()
    {
        var result = RoundTrip(new NetworkSiegeKeepEntryRequest("request-1", "map-event-1", true,
            Array.Empty<HideoutTroopSelectionEntry>()));

        Assert.True(result.QueryOnly);
        Assert.Null(result.Troops);
    }

    [Fact]
    public void StartSiegeMission_RoundTripPreservesTheKeepFlag()
    {
        var original = new NetworkStartSiegeMission("map-event-1", 2, new[] { 1f },
            Array.Empty<SiegeEngineState>(), Array.Empty<SiegeEngineState>(), "party-1", "town_ES1",
            isLordsHall: true);

        var result = RoundTrip(original);

        Assert.True(result.IsLordsHall);
        Assert.False(result.IsSallyOut);
        Assert.Equal(original.SettlementId, result.SettlementId);
    }

    private static T RoundTrip<T>(T original)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }
}
