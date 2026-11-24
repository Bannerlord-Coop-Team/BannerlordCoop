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
    public void EntryReply_RoundTripPreservesAllowanceSelectionAndLock()
    {
        var original = new NetworkSiegeKeepEntryReply("request-1", true, null, 6, 42,
            new[] { new HideoutTroopSelectionEntry("player-hero", 1), new HideoutTroopSelectionEntry("escort", 6) },
            isLocked: true);

        var result = RoundTrip(original);

        Assert.Equal(original.RequestId, result.RequestId);
        Assert.True(result.Accepted);
        Assert.Equal(6, result.EscortAllowance);
        Assert.Equal(42, result.SecondsRemaining);
        Assert.Equal(original.Selection, result.Selection);
        Assert.True(result.IsLocked);
    }

    [Fact]
    public void PhaseChanged_RoundTripPreservesPhaseAndSeat()
    {
        var original = new NetworkSiegeKeepPhaseChanged("map-event-1", SiegeKeepPhase.Locked, true, 4, 0);

        var result = RoundTrip(original);

        Assert.Equal(original.MapEventId, result.MapEventId);
        Assert.Equal(SiegeKeepPhase.Locked, result.Phase);
        Assert.True(result.IsSeated);
        Assert.Equal(4, result.EscortAllowance);
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
