using GameInterface.Services.Locations.Messages;
using Missions.Locations;
using Missions.Messages;
using System;
using Xunit;

namespace Coop.Tests.Missions.Locations;

public class LocationAgentBindingMapTests
{
    private readonly LocationAgentBindingMap map = new();

    [Fact]
    public void Record_TryGet_Forget_RoundTrip()
    {
        var agentId = Guid.NewGuid();
        var rosterEntry = new LocationCharacterData(
            "settlement1_center", "townsman_empire", null, null, "npc_common", null, null, 0, false, true);

        map.Record(agentId, new LocationAgentBinding(LocationAgentKind.Human, rosterEntry));

        Assert.Equal(1, map.Count);
        Assert.True(map.TryGet(agentId, out var binding));
        Assert.Equal(LocationAgentKind.Human, binding.Kind);
        Assert.Same(rosterEntry, binding.RosterEntry);

        map.Forget(agentId);
        Assert.Equal(0, map.Count);
        Assert.False(map.TryGet(agentId, out _));
    }

    [Fact]
    public void AnimalBinding_CarriesItemIdentities()
    {
        var agentId = Guid.NewGuid();
        map.Record(agentId, new LocationAgentBinding(LocationAgentKind.Animal, null, "sheep", "harness_a"));

        Assert.True(map.TryGet(agentId, out var binding));
        Assert.Equal(LocationAgentKind.Animal, binding.Kind);
        Assert.Null(binding.RosterEntry);
        Assert.Equal("sheep", binding.ItemId);
        Assert.Equal("harness_a", binding.HarnessItemId);
    }

    [Fact]
    public void EmptyIdOrNullBinding_IsIgnored()
    {
        map.Record(Guid.Empty, new LocationAgentBinding(LocationAgentKind.Human, null));
        map.Record(Guid.NewGuid(), null);

        Assert.Equal(0, map.Count);
    }
}
