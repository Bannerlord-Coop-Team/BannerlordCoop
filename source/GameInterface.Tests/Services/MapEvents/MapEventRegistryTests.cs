using GameInterface.Services.MapEvents;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventRegistryTests
{
    [Fact]
    public void NormalizeLoadedMapEventIds_AssignsMissingIdsAndPreservesExistingIds()
    {
        var existing = CreateMapEvent("existing_map_event");
        var firstMissing = CreateMapEvent();
        var secondMissing = CreateMapEvent();

        MapEventRegistry.NormalizeLoadedMapEventIds(new List<MapEvent>
        {
            existing,
            firstMissing,
            secondMissing,
        });

        Assert.Equal("existing_map_event", existing.StringId);
        Assert.Equal($"{MapEventRegistry.LoadedMapEventIdPrefix}0", firstMissing.StringId);
        Assert.Equal($"{MapEventRegistry.LoadedMapEventIdPrefix}1", secondMissing.StringId);
    }

    [Fact]
    public void NormalizeLoadedMapEventIds_SkipsIdsAlreadyUsedByLoadedEvents()
    {
        var existing = CreateMapEvent($"{MapEventRegistry.LoadedMapEventIdPrefix}0");
        var missing = CreateMapEvent();

        MapEventRegistry.NormalizeLoadedMapEventIds(new[] { existing, missing });

        Assert.Equal($"{MapEventRegistry.LoadedMapEventIdPrefix}1", missing.StringId);
    }

    [Fact]
    public void NormalizeLoadedMapEventIds_IsIdempotent()
    {
        var mapEvent = CreateMapEvent();
        var mapEvents = new[] { mapEvent };

        MapEventRegistry.NormalizeLoadedMapEventIds(mapEvents);
        string assignedId = mapEvent.StringId;

        MapEventRegistry.NormalizeLoadedMapEventIds(mapEvents);

        Assert.Equal(assignedId, mapEvent.StringId);
    }

    private static MapEvent CreateMapEvent(string? stringId = null)
    {
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        mapEvent.StringId = stringId;
        return mapEvent;
    }
}
