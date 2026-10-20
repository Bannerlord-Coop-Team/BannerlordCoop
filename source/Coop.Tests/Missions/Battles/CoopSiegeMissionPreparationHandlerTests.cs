using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Covers the wall-dressing seed every client derives from the shared map-event id.</summary>
public class CoopSiegeMissionPreparationHandlerTests
{
    /// <summary>Two clients opening the same siege must break the same wall pieces.</summary>
    [Fact]
    public void SameMapEventId_ProducesTheSameSeed()
    {
        var first = NewHandler("shared-map-event");
        var second = NewHandler("shared-map-event");

        Assert.Equal(first.SceneSeed, second.SceneSeed);
    }

    /// <summary>Different sieges should not share one wall-dressing pattern.</summary>
    [Fact]
    public void DifferentMapEventIds_ProduceDifferentSeeds()
    {
        var first = NewHandler("map-event-17");
        var second = NewHandler("map-event-18");

        Assert.NotEqual(first.SceneSeed, second.SceneSeed);
    }

    /// <summary>The seed is never zero, including for an empty id.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("map-event-17")]
    public void Seed_IsNeverZero(string mapEventId)
    {
        Assert.NotEqual(0u, NewHandler(mapEventId).SceneSeed);
    }

    /// <summary>Builds a walls-assault handler for the given map event.</summary>
    private static CoopSiegeMissionPreparationHandler NewHandler(string mapEventId) =>
        new CoopSiegeMissionPreparationHandler(false, new[] { 1f, 1f }, false, mapEventId);
}
