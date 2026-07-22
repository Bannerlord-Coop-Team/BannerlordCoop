using Missions.Battles;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CasualtyAttributionMapTests
{
    private readonly CasualtyAttributionMap map = new();

    [Fact]
    public void Record_ThenGet_RoundTrips()
    {
        var agentId = Guid.NewGuid();
        map.Record(agentId, "mapEventParty1", 42, "character1");

        var attribution = map.GetOrDefault(agentId);
        Assert.Equal("mapEventParty1", attribution.MapEventPartyId);
        Assert.Equal(42, attribution.TroopSeed);
        Assert.Equal("character1", attribution.TroopCharacterId);
    }

    [Fact]
    public void GetOrDefault_UnknownAgent_IsTheEmptyDefault()
    {
        // Callers rely on null ids (skip the casualty report) rather than a throw or a TryGet dance.
        var attribution = map.GetOrDefault(Guid.NewGuid());
        Assert.Null(attribution.MapEventPartyId);
        Assert.Equal(0, attribution.TroopSeed);
        Assert.Null(attribution.TroopCharacterId);
    }

    [Fact]
    public void Record_SameAgent_Overwrites()
    {
        var agentId = Guid.NewGuid();
        map.Record(agentId, "party1", 1, "char1");
        map.Record(agentId, "party2", 2, "char2");

        var attribution = map.GetOrDefault(agentId);
        Assert.Equal("party2", attribution.MapEventPartyId);
        Assert.Equal(2, attribution.TroopSeed);
        Assert.Equal("char2", attribution.TroopCharacterId);
    }

    [Fact]
    public void Forget_RemovesTheAttribution()
    {
        var agentId = Guid.NewGuid();
        map.Record(agentId, "party1", 1, "char1");

        map.Forget(agentId);

        Assert.Null(map.GetOrDefault(agentId).MapEventPartyId);

        // Forgetting an unknown/already-forgotten agent is a no-op, not a throw (death + despawn can both forget).
        map.Forget(agentId);
    }

    [Fact]
    public void MarkDeparted_RemembersTheReserveSeedAfterRemovingAgentAttribution()
    {
        var agentId = Guid.NewGuid();
        map.Record(agentId, "party1", 1949, "char1");

        map.MarkDeparted(agentId);

        Assert.Null(map.GetOrDefault(agentId).MapEventPartyId);
        Assert.True(map.WasDeparted(1949));
        Assert.False(map.WasDeparted(1950));
    }
}
