using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Covers the wall-dressing seed every client derives from the shared map-event id.</summary>
public class CoopSiegeMissionPreparationHandlerTests
{
    /// <summary>Two clients opening the same siege must break the same wall pieces, so the seed is pinned to the
    /// cross-process FNV-1a value instead of a per-process string.GetHashCode.</summary>
    [Fact]
    public void SameMapEventId_ProducesTheSameSeedOnEveryProcess()
    {
        Assert.Equal(160666638u, NewHandler("shared-map-event").SceneSeed);
    }

    /// <summary>Different sieges should not share one wall-dressing pattern.</summary>
    [Fact]
    public void DifferentMapEventIds_ProduceDifferentSeeds()
    {
        var first = NewHandler("map-event-17");
        var second = NewHandler("map-event-18");

        Assert.NotEqual(first.SceneSeed, second.SceneSeed);
    }

    /// <summary>Builds a walls-assault handler for the given map event.</summary>
    private static CoopSiegeMissionPreparationHandler NewHandler(string mapEventId) =>
        new CoopSiegeMissionPreparationHandler(false, new[] { 1f, 1f }, false, mapEventId);
}
