using System;
using Common.Serialization;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Messages;
using Xunit;

namespace GameInterface.Tests.Serialization;

// Verifies siege graph snapshots survive the wire inside both messages that carry them.
public class SiegeEventGraphSerializationTest
{
    // Round-trips the init and map-event commit messages and checks every graph id survives.
    [Fact]
    public void SiegeGraphMessages_RoundTripAllIds()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var snapshot = new SiegeEventGraphSnapshot(
            "siege-event",
            101,
            102,
            "camp",
            103,
            104,
            "attacker-engines",
            105,
            "defender-engines",
            106,
            1234,
            "strategy",
            7,
            new uint[] { 104, 107 },
            new[]
            {
                new SiegeEngineGraphSnapshot(
                    "preparation", 108, "preparations", 0.75f, 1f, 100f, 100f,
                    SiegeEngineGraphLocation.Preparation),
            },
            Array.Empty<SiegeEngineGraphSnapshot>());

        var initialization = RoundTrip(serializer, new NetworkInitializeSiegeEvent(snapshot));
        AssertGraph(snapshot, initialization.ToSnapshot());

        var mapCommit = RoundTrip(serializer, new NetworkMapEventInitialized(
            201, false, 202, 203, 204, snapshot));
        AssertGraph(snapshot, mapCommit.SiegeGraph);
    }

    // Serializes and deserializes a message with the production serializer.
    private static T RoundTrip<T>(ProtoBufSerializer serializer, T message)
    {
        return serializer.Deserialize<T>(serializer.Serialize(message));
    }

    // Asserts every field of two graph snapshots matches.
    private static void AssertGraph(SiegeEventGraphSnapshot expected, SiegeEventGraphSnapshot actual)
    {
        Assert.Equal(expected.SiegeEventId, actual.SiegeEventId);
        Assert.Equal(expected.SiegeEventHandle, actual.SiegeEventHandle);
        Assert.Equal(expected.SettlementHandle, actual.SettlementHandle);
        Assert.Equal(expected.BesiegerCampId, actual.BesiegerCampId);
        Assert.Equal(expected.BesiegerCampHandle, actual.BesiegerCampHandle);
        Assert.Equal(expected.LeaderPartyHandle, actual.LeaderPartyHandle);
        Assert.Equal(expected.AttackerSiegeEnginesId, actual.AttackerSiegeEnginesId);
        Assert.Equal(expected.AttackerSiegeEnginesHandle, actual.AttackerSiegeEnginesHandle);
        Assert.Equal(expected.DefenderSiegeEnginesId, actual.DefenderSiegeEnginesId);
        Assert.Equal(expected.DefenderSiegeEnginesHandle, actual.DefenderSiegeEnginesHandle);
        Assert.Equal(expected.SiegeStartTimeTicks, actual.SiegeStartTimeTicks);
        Assert.Equal(expected.BesiegerStrategyId, actual.BesiegerStrategyId);
        Assert.Equal(expected.BesiegerTroopsKilled, actual.BesiegerTroopsKilled);
        Assert.Equal(expected.BesiegerPartyHandles, actual.BesiegerPartyHandles);
        Assert.Equal(expected.AttackerEngines, actual.AttackerEngines);
        Assert.Equal(expected.DefenderEngines ?? Array.Empty<SiegeEngineGraphSnapshot>(),
            actual.DefenderEngines ?? Array.Empty<SiegeEngineGraphSnapshot>());
    }
}
