using Common.Util;
using GameInterface.Services.MapEventSides.Handlers;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventSideDataHandlerTests
{
    [Fact]
    public void ShouldAdoptPlayerEncounterMapEvent_WhenMainPartyMatchesAndEncounterIsUnbound()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();

        Assert.True(MapEventSideDataHandler.ShouldAdoptPlayerEncounterMapEvent(mapEvent, mapEvent, null));
    }

    [Fact]
    public void ShouldNotAdoptPlayerEncounterMapEvent_WhenMainPartyDoesNotMatch()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        var otherMapEvent = ObjectHelper.SkipConstructor<MapEvent>();

        Assert.False(MapEventSideDataHandler.ShouldAdoptPlayerEncounterMapEvent(mapEvent, otherMapEvent, null));
    }

    [Fact]
    public void ShouldNotAdoptPlayerEncounterMapEvent_WhenEncounterIsAlreadyBound()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();

        Assert.False(MapEventSideDataHandler.ShouldAdoptPlayerEncounterMapEvent(mapEvent, mapEvent, mapEvent));
    }
}
