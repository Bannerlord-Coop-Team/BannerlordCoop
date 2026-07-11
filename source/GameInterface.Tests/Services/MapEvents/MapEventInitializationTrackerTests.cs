using Common.Util;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventInitializationTrackerTests
{
    [Fact]
    public void CompleteBuild_ReleasesEveryObjectOwnedByTheInitialGraph()
    {
        var objectManager = CreateObjectManager();
        var tracker = new MapEventInitializationTracker(objectManager);
        var mapEvent = New<MapEvent>();
        var replacedUpgradeTracker = New<TroopUpgradeTracker>();
        var finalUpgradeTracker = New<TroopUpgradeTracker>();

        using (tracker.BeginMapEventConstruction())
        {
            Assert.True(tracker.TryDefer(mapEvent, Array.Empty<object>()));
            Assert.True(tracker.TryDefer(replacedUpgradeTracker, Array.Empty<object>()));
        }

        tracker.BeginBuild(mapEvent);

        // Vanilla replaces the constructor-created tracker when the main party is attached. Its
        // no-argument constructor must remain owned by the same aggregate transaction.
        using (tracker.BeginGraphChildConstruction(mapEvent))
        {
            Assert.True(tracker.TryDefer(finalUpgradeTracker, Array.Empty<object>()));
        }

        var replacedVisual = NewGauntletMapEventVisual();
        var finalVisual = NewGauntletMapEventVisual();
        var component = New<FieldBattleEventComponent>();
        var side = New<MapEventSide>();
        LinkSideToMapEvent(side, mapEvent);

        Assert.True(tracker.TryDefer(replacedVisual, new object[] { mapEvent }));
        Assert.True(tracker.TryDefer(finalVisual, new object[] { mapEvent }));
        Assert.True(tracker.TryDefer(component, new object[] { mapEvent }));
        Assert.True(tracker.TryDefer(side, new object[] { mapEvent }));

        var partyBase = New<PartyBase>();
        LinkPartyToSide(partyBase, side);
        var mapEventParty = New<MapEventParty>();
        Assert.True(tracker.TryDefer(mapEventParty, new object[] { partyBase }));

        var replacedRoster = New<TroopRoster>();
        var finalRosters = new[]
        {
            New<TroopRoster>(),
            New<TroopRoster>(),
            New<TroopRoster>(),
        };

        using (tracker.BeginMapEventPartyConstruction(partyBase))
        {
            Assert.True(tracker.TryDefer(replacedRoster, Array.Empty<object>()));
            foreach (var roster in finalRosters)
            {
                Assert.True(tracker.TryDefer(roster, Array.Empty<object>()));
            }
        }

        var allOwnedObjects = new List<object>
        {
            mapEvent,
            replacedUpgradeTracker,
            finalUpgradeTracker,
            replacedVisual,
            finalVisual,
            component,
            side,
            mapEventParty,
            replacedRoster,
        };
        allOwnedObjects.AddRange(finalRosters);

        for (int i = 0; i < allOwnedObjects.Count; i++)
            Assert.True(objectManager.AddExisting($"owned_{i}", allOwnedObjects[i]));

        Assert.True(tracker.IsBuilding(mapEvent));
        Assert.All(allOwnedObjects, instance => Assert.True(tracker.IsPending(instance)));

        // The completed graph intentionally excludes constructor-created objects which were replaced.
        // They still belong to the root and must not remain pending after the transaction commits.
        tracker.CompleteBuild(mapEvent, new object[]
        {
            mapEvent,
            finalUpgradeTracker,
            finalVisual,
            component,
            side,
            mapEventParty,
            finalRosters[0],
            finalRosters[1],
            finalRosters[2],
        });

        Assert.False(tracker.IsBuilding(mapEvent));
        Assert.All(allOwnedObjects, instance => Assert.False(tracker.IsPending(instance)));

        Assert.False(objectManager.Contains(replacedUpgradeTracker));
        Assert.False(objectManager.Contains(replacedVisual));
        Assert.False(objectManager.Contains(replacedRoster));
        Assert.True(objectManager.Contains(mapEvent));
        Assert.True(objectManager.Contains(finalUpgradeTracker));
        Assert.True(objectManager.Contains(finalVisual));

        tracker.DestroyGraph(mapEvent);
        Assert.All(allOwnedObjects, instance => Assert.False(objectManager.Contains(instance)));
    }

    [Fact]
    public void CompletedRoot_OwnsLateChildrenWithoutDeferringTheirLifetimePackets()
    {
        var objectManager = CreateObjectManager();
        var tracker = new MapEventInitializationTracker(objectManager);
        var mapEvent = New<MapEvent>();
        Assert.True(tracker.TryDefer(mapEvent, Array.Empty<object>()));
        tracker.BeginBuild(mapEvent);
        tracker.CompleteBuild(mapEvent, new object[] { mapEvent });
        Assert.True(objectManager.AddExisting("MapEvent_root", mapEvent));

        var side = New<MapEventSide>();
        LinkSideToMapEvent(side, mapEvent);
        var reinforcementPartyBase = New<PartyBase>();
        LinkPartyToSide(reinforcementPartyBase, side);
        var reinforcementParty = New<MapEventParty>();
        var reinforcementRosters = new[]
        {
            New<TroopRoster>(),
            New<TroopRoster>(),
            New<TroopRoster>(),
        };
        var replacementUpgradeTracker = New<TroopUpgradeTracker>();

        Assert.True(objectManager.AddExisting("MapEventParty_reinforcement", reinforcementParty));
        Assert.False(tracker.TryDefer(reinforcementParty, new object[] { reinforcementPartyBase }));
        using (tracker.BeginMapEventPartyConstruction(reinforcementPartyBase))
        {
            for (int i = 0; i < reinforcementRosters.Length; i++)
            {
                Assert.True(objectManager.AddExisting($"TroopRoster_reinforcement_{i}", reinforcementRosters[i]));
                Assert.False(tracker.TryDefer(reinforcementRosters[i], Array.Empty<object>()));
            }
        }

        using (tracker.BeginGraphChildConstruction(mapEvent))
        {
            Assert.True(objectManager.AddExisting("TroopUpgradeTracker_replacement", replacementUpgradeTracker));
            Assert.False(tracker.TryDefer(replacementUpgradeTracker, Array.Empty<object>()));
        }

        var standalonePartyBase = New<PartyBase>();
        var standaloneParty = New<MapEventParty>();
        var standaloneRoster = New<TroopRoster>();
        var standaloneUpgradeTracker = New<TroopUpgradeTracker>();

        Assert.True(objectManager.AddExisting("MapEventParty_standalone", standaloneParty));
        Assert.True(objectManager.AddExisting("TroopRoster_standalone", standaloneRoster));
        Assert.True(objectManager.AddExisting("TroopUpgradeTracker_standalone", standaloneUpgradeTracker));
        Assert.False(tracker.TryDefer(standaloneParty, new object[] { standalonePartyBase }));
        using (tracker.BeginMapEventPartyConstruction(standalonePartyBase))
        {
            Assert.False(tracker.TryDefer(standaloneRoster, Array.Empty<object>()));
        }

        using (tracker.BeginGraphChildConstruction(New<MapEvent>()))
        {
            Assert.False(tracker.TryDefer(standaloneUpgradeTracker, Array.Empty<object>()));
        }

        Assert.False(tracker.TryDefer(New<FieldBattleEventComponent>(), new object[] { mapEvent }));
        Assert.False(tracker.TryDefer(NewGauntletMapEventVisual(), new object[] { mapEvent }));

        tracker.DestroyGraph(mapEvent);

        Assert.False(objectManager.Contains(mapEvent));
        Assert.False(objectManager.Contains(reinforcementParty));
        Assert.All(reinforcementRosters, roster => Assert.False(objectManager.Contains(roster)));
        Assert.False(objectManager.Contains(replacementUpgradeTracker));
        Assert.True(objectManager.Contains(standaloneParty));
        Assert.True(objectManager.Contains(standaloneRoster));
        Assert.True(objectManager.Contains(standaloneUpgradeTracker));
    }

    [Fact]
    public void ExtendCommittedGraph_OwnsClientHydratedChildrenAndIgnoresUntrackedRoots()
    {
        var objectManager = CreateObjectManager();
        var tracker = new MapEventInitializationTracker(objectManager);
        var mapEvent = New<MapEvent>();
        var reinforcementParty = New<MapEventParty>();
        var reinforcementRosters = new[]
        {
            New<TroopRoster>(),
            New<TroopRoster>(),
            New<TroopRoster>(),
        };
        var firstReplacementTracker = New<TroopUpgradeTracker>();
        var secondReplacementTracker = New<TroopUpgradeTracker>();
        var untrackedMapEvent = New<MapEvent>();
        var standaloneRoster = New<TroopRoster>();

        tracker.RegisterCommittedGraph(mapEvent, new object[] { mapEvent });
        Assert.True(objectManager.AddExisting("MapEvent_root", mapEvent));
        Assert.True(objectManager.AddExisting("MapEventParty_reinforcement", reinforcementParty));
        for (int i = 0; i < reinforcementRosters.Length; i++)
            Assert.True(objectManager.AddExisting($"TroopRoster_reinforcement_{i}", reinforcementRosters[i]));
        Assert.True(objectManager.AddExisting("TroopUpgradeTracker_first", firstReplacementTracker));
        Assert.True(objectManager.AddExisting("TroopUpgradeTracker_second", secondReplacementTracker));
        Assert.True(objectManager.AddExisting("TroopRoster_standalone", standaloneRoster));

        tracker.ExtendCommittedGraph(mapEvent, new object[]
        {
            reinforcementParty,
            reinforcementRosters[0],
            reinforcementRosters[1],
            reinforcementRosters[2],
            reinforcementParty,
            reinforcementRosters[0],
        });
        tracker.ExtendCommittedGraph(mapEvent, reinforcementRosters);
        tracker.ExtendCommittedGraph(mapEvent, new object[] { firstReplacementTracker });
        tracker.ExtendCommittedGraph(mapEvent, new object[] { secondReplacementTracker });
        tracker.ExtendCommittedGraph(untrackedMapEvent, new object[] { standaloneRoster });

        // RegisterAllObjects runs again whenever a joining peer collects its id remap. Re-adopting the
        // current live graph must union with, rather than overwrite, earlier ownership history.
        tracker.RegisterCommittedGraph(mapEvent, new object[] { mapEvent });

        tracker.DestroyGraph(mapEvent);

        Assert.False(objectManager.Contains(mapEvent));
        Assert.False(objectManager.Contains(reinforcementParty));
        Assert.All(reinforcementRosters, roster => Assert.False(objectManager.Contains(roster)));
        Assert.False(objectManager.Contains(firstReplacementTracker));
        Assert.False(objectManager.Contains(secondReplacementTracker));
        Assert.True(objectManager.Contains(standaloneRoster));
    }

    [Fact]
    public void DestroyGraph_MergesTheCurrentReachableGraphWithTheCommittedSnapshot()
    {
        var objectManager = CreateObjectManager();
        var tracker = new MapEventInitializationTracker(objectManager);
        var mapEvent = New<MapEvent>();
        var originalUpgradeTracker = New<TroopUpgradeTracker>();
        var replacementUpgradeTracker = New<TroopUpgradeTracker>();
        var standaloneUpgradeTracker = New<TroopUpgradeTracker>();

        mapEvent.TroopUpgradeTracker = replacementUpgradeTracker;
        tracker.RegisterCommittedGraph(mapEvent, new object[] { mapEvent, originalUpgradeTracker });

        Assert.True(objectManager.AddExisting("MapEvent_root", mapEvent));
        Assert.True(objectManager.AddExisting("TroopUpgradeTracker_original", originalUpgradeTracker));
        Assert.True(objectManager.AddExisting("TroopUpgradeTracker_replacement", replacementUpgradeTracker));
        Assert.True(objectManager.AddExisting("TroopUpgradeTracker_standalone", standaloneUpgradeTracker));

        tracker.DestroyGraph(mapEvent);

        Assert.False(objectManager.Contains(mapEvent));
        Assert.False(objectManager.Contains(originalUpgradeTracker));
        Assert.False(objectManager.Contains(replacementUpgradeTracker));
        Assert.True(objectManager.Contains(standaloneUpgradeTracker));
    }

    [Fact]
    public void AbortBuild_RemovesSuppressedRegistrationsAndExternalEdges()
    {
        var objectManager = CreateObjectManager();
        var tracker = new MapEventInitializationTracker(objectManager);
        var mapEvent = New<MapEvent>();
        var visual = new Mock<IMapEventVisual>();
        mapEvent.MapEventVisual = visual.Object;
        Assert.True(tracker.TryDefer(mapEvent, Array.Empty<object>()));
        tracker.BeginBuild(mapEvent);

        var side = New<MapEventSide>();
        LinkSideToMapEvent(side, mapEvent);
        Assert.True(tracker.TryDefer(side, new object[] { mapEvent }));

        var partyBase = New<PartyBase>();
        LinkPartyToSide(partyBase, side);
        var mapEventParty = New<MapEventParty>();
        SetField(mapEventParty, "<Party>k__BackingField", partyBase);
        Assert.True(tracker.TryDefer(mapEventParty, new object[] { partyBase }));

        Assert.True(objectManager.AddExisting("MapEvent_root", mapEvent));
        Assert.True(objectManager.AddExisting("MapEventSide_side", side));
        Assert.True(objectManager.AddExisting("MapEventParty_party", mapEventParty));
        Assert.True(objectManager.AddExisting("MapEventVisual_visual", visual.Object));

        tracker.AbortBuild(mapEvent);

        Assert.Null(partyBase.MapEventSide);
        Assert.False(tracker.IsBuilding(mapEvent));
        Assert.False(tracker.IsInitializing(mapEvent));
        Assert.False(objectManager.Contains(mapEvent));
        Assert.False(objectManager.Contains(side));
        Assert.False(objectManager.Contains(mapEventParty));
        Assert.False(objectManager.Contains(visual.Object));
        visual.Verify(instance => instance.OnMapEventEnd(), Times.Once);
    }

    [Fact]
    public void PublishedDuringInitialize_RemainsDeferredUntilInitializeExits()
    {
        var objectManager = CreateObjectManager();
        var tracker = new MapEventInitializationTracker(objectManager);
        var mapEvent = New<MapEvent>();
        var child = New<TroopUpgradeTracker>();

        using (tracker.BeginMapEventConstruction())
        {
            Assert.True(tracker.TryDefer(mapEvent, Array.Empty<object>()));
            Assert.True(tracker.TryDefer(child, Array.Empty<object>()));
        }

        tracker.BeginBuild(mapEvent);
        Assert.True(objectManager.AddExisting("MapEvent_root", mapEvent));
        Assert.True(objectManager.AddExisting("TroopUpgradeTracker_child", child));

        tracker.MarkPublished(mapEvent, new object[] { mapEvent, child });

        Assert.True(tracker.IsPublished(mapEvent));
        Assert.True(tracker.IsInitializing(mapEvent));
        Assert.True(tracker.IsBuilding(mapEvent));
        Assert.True(tracker.IsPending(mapEvent));
        Assert.True(tracker.IsPending(child));

        tracker.EndInitialization(mapEvent);
        tracker.CompletePublishedBuild(mapEvent);

        Assert.False(tracker.IsPublished(mapEvent));
        Assert.False(tracker.IsInitializing(mapEvent));
        Assert.False(tracker.IsBuilding(mapEvent));
        Assert.False(tracker.IsPending(mapEvent));
        Assert.False(tracker.IsPending(child));
        Assert.True(objectManager.Contains(mapEvent));
        Assert.True(objectManager.Contains(child));
    }

    private static IObjectManager CreateObjectManager() =>
        new global::GameInterface.Services.ObjectManager.ObjectManager(Mock.Of<ILogger>());

    private static T New<T>() where T : class => ObjectHelper.SkipConstructor<T>();

    private static object NewGauntletMapEventVisual()
    {
        var visualType = Type.GetType(
            "SandBox.GauntletUI.Map.GauntletMapEventVisual, SandBox.GauntletUI",
            throwOnError: true);

        return ObjectHelper.SkipConstructor(visualType!);
    }

    private static void LinkSideToMapEvent(MapEventSide side, MapEvent mapEvent) =>
        SetField(side, "_mapEvent", mapEvent);

    private static void LinkPartyToSide(PartyBase party, MapEventSide side) =>
        SetField(party, "_mapEventSide", side);

    private static void SetField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
