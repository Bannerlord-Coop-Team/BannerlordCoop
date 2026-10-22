using Common.Util;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class OrphanedMapEventFinderTests
{
    private static readonly IOrphanedMapEventFinder Finder = new OrphanedMapEventFinder();

    private static MapEvent CreateMapEvent(MapEventState state)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent._state = state;
        return mapEvent;
    }

    private static PartyBase CreateLinkedParty(MapEvent? mapEvent)
    {
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        var side = ObjectHelper.SkipConstructor<MapEventSide>();
        AccessTools.Field(typeof(MapEventSide), "_mapEvent").SetValue(side, mapEvent);
        party._mapEventSide = side;
        return party;
    }

    [Fact]
    public void Find_FinalizedUnlistedEventReferencedByTwoParties_ReturnedOnceWithBothParties()
    {
        var mapEvent = CreateMapEvent(MapEventState.WaitingRemoval);
        var first = CreateLinkedParty(mapEvent);
        var second = CreateLinkedParty(mapEvent);

        var found = Finder.Find(new[] { first, second }, new HashSet<MapEvent>());

        Assert.Same(mapEvent, Assert.Single(found.Finalized));
        Assert.Empty(found.UnlistedLive);
        Assert.Equal(new[] { first, second }, found.LinkedParties(mapEvent));
    }

    [Fact]
    public void Find_FinalizedButStillListedEvent_IsReturnedAsFinalized()
    {
        var mapEvent = CreateMapEvent(MapEventState.WaitingRemoval);
        var party = CreateLinkedParty(mapEvent);

        var found = Finder.Find(new[] { party }, new HashSet<MapEvent> { mapEvent });

        Assert.Same(mapEvent, Assert.Single(found.Finalized));
        Assert.Empty(found.UnlistedLive);
    }

    [Fact]
    public void Find_LiveListedEvent_IsReturnedNowhere()
    {
        var mapEvent = CreateMapEvent(MapEventState.Wait);
        var party = CreateLinkedParty(mapEvent);

        var found = Finder.Find(new[] { party }, new HashSet<MapEvent> { mapEvent });

        Assert.Empty(found.Finalized);
        Assert.Empty(found.UnlistedLive);
    }

    [Fact]
    public void Find_LiveUnlistedEvent_IsReturnedOnlyAsUnlistedLive()
    {
        var mapEvent = CreateMapEvent(MapEventState.Wait);
        var party = CreateLinkedParty(mapEvent);

        var found = Finder.Find(new[] { party }, new HashSet<MapEvent>());

        Assert.Empty(found.Finalized);
        Assert.Same(mapEvent, Assert.Single(found.UnlistedLive));
    }

    [Fact]
    public void Find_NullSideAndNullEvent_AreSkipped()
    {
        var noSide = ObjectHelper.SkipConstructor<PartyBase>();
        var noEvent = CreateLinkedParty(null);

        var found = Finder.Find(new PartyBase?[] { null, noSide, noEvent }, new HashSet<MapEvent>());

        Assert.Empty(found.Finalized);
        Assert.Empty(found.UnlistedLive);
    }
}
