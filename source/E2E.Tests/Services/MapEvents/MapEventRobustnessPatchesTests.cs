using Common.Util;
using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Regression coverage for MapEvent tracker recovery after a save restores a null tracker.
/// </summary>
public class MapEventRobustnessPatchesTests : MapEventTestBase
{
    public MapEventRobustnessPatchesTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Server_TroopUpgradeTrackerRecovery_RehydratesEveryClient()
    {
        var context = CreateServerMapEvent();
        string? trackerId = null;

        Server.Call(() =>
        {
            var mapEvent = GetMapEvent(Server, context.MapEventId);

            // Emulate the server-only null left by save deserialization without broadcasting null to clients.
            using (new AllowedThread()) mapEvent.TroopUpgradeTracker = null;

            var restored = mapEvent.TroopUpgradeTracker;
            Assert.NotNull(restored);
            Assert.True(Server.ObjectManager.TryGetId(restored, out trackerId));
            AssertTrackerMatchesSides(mapEvent, restored);
        }, MapEventDisabledMethods);

        Assert.NotNull(trackerId);
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                var mapEvent = GetMapEvent(client, context.MapEventId);
                Assert.True(client.ObjectManager.TryGetObject<TroopUpgradeTracker>(trackerId, out var restored));
                Assert.Same(restored, mapEvent.TroopUpgradeTracker);
                AssertTrackerMatchesSides(mapEvent, restored);
            });
        }
    }

    [Fact]
    public void Client_NullTroopUpgradeTracker_WaitsForAuthoritativeReplacement()
    {
        var context = CreateServerMapEvent();
        var client = Clients.First();

        client.Call(() =>
        {
            var mapEvent = GetMapEvent(client, context.MapEventId);

            using (new AllowedThread()) mapEvent.TroopUpgradeTracker = null;

            Assert.Null(mapEvent.TroopUpgradeTracker);
        });
    }

    [Fact]
    public void Server_FinalizedMapEventTrackerRecovery_DoesNotRehydrateDiscardedParties()
    {
        var context = CreateServerMapEvent();

        Server.Call(() =>
        {
            var mapEvent = GetMapEvent(Server, context.MapEventId);
            mapEvent.State = MapEventState.WaitingRemoval;
            using (new AllowedThread()) mapEvent.TroopUpgradeTracker = null;

            var restored = mapEvent.TroopUpgradeTracker;

            Assert.NotNull(restored);
            Assert.Empty(restored._mapEventParties);
        }, MapEventDisabledMethods);
    }

    private static MapEvent GetMapEvent(EnvironmentInstance instance, string mapEventId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
        return mapEvent;
    }

    private static void AssertTrackerMatchesSides(MapEvent mapEvent, TroopUpgradeTracker tracker)
    {
        var involvedParties = mapEvent._sides
            .SelectMany(side => side.Parties)
            .ToList();

        Assert.Equal(involvedParties.Count, tracker._mapEventParties.Count);
        Assert.All(involvedParties, party => Assert.Contains(party, tracker._mapEventParties));
    }
}
