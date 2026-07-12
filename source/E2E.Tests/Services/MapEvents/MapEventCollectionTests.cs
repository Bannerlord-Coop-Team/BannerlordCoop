using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventCollectionTests : MapEventTestBase
{
    public MapEventCollectionTests(ITestOutputHelper output) : base(output) { }

    [Fact(Skip = "MapEvent._sides is a fixed-size MapEventSide[2] array")]
    public void Server_MapEvent_Sides_IsFixedArray() { }

    [Fact]
    public void Server_MapEvent_CommitPublishesCompleteLockedGraph()
    {
        Server.NetworkSentMessages.Clear();
        var staged = CreateServerMapEvent(commit: false);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMapEventInitialized>());
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkMapEventPartyPending>().Count());
        var messages = Server.NetworkSentMessages.Messages.ToList();
        Assert.True(messages.FindLastIndex(x => x is NetworkMapEventPartyPending) <
            messages.FindIndex(x => x is NetworkAssignMapEventSide));
        Server.Call(() => Assert.False(PendingMapEventPartyMovementPatch.CanAdvancePosition(
            Get<MobileParty>(Server, staged.DefenderPartyId).Party)));
        foreach (var client in Clients) AssertPending(client, staged, true);

        Server.Call(() => Campaign.Current.MapEventManager.OnMapEventCreated(
            Get<MapEvent>(Server, staged.MapEventId)), MapEventDisabledMethods);

        var marker = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventInitialized>());
        Assert.False(marker.IsTerminal);
        Assert.NotNull(marker.TroopUpgradeTrackerId);
        Assert.NotNull(marker.ComponentId);
        foreach (var instance in AllInstances) AssertCommitted(instance, staged);
        foreach (var client in Clients) AssertPending(client, staged, false);
    }

    [Fact]
    public void Server_MapEvent_AbortDestroysStagedGraph()
    {
        Server.NetworkSentMessages.Clear();
        var staged = CreateServerMapEvent(commit: false);

        Server.Call(() =>
        {
            var mapEvent = Get<MapEvent>(Server, staged.MapEventId);
            Server.Resolve<IMapEventInitializationBarrier>().AbortServer(mapEvent);
        }, MapEventDisabledMethods);

        Assert.True(Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventInitialized>()).IsTerminal);
        foreach (var instance in AllInstances) instance.Call(() =>
        {
            Assert.False(instance.ObjectManager.Contains(staged.MapEventId));
            Assert.Null(Get<MobileParty>(instance, staged.AttackerPartyId).MapEvent);
            Assert.Null(Get<MobileParty>(instance, staged.DefenderPartyId).MapEvent);
        });
    }

    private static void AssertCommitted(EnvironmentInstance instance, MapEventContext staged) => instance.Call(() =>
    {
        var mapEvent = Get<MapEvent>(instance, staged.MapEventId);
        var attacker = Get<MobileParty>(instance, staged.AttackerPartyId);
        var defender = Get<MobileParty>(instance, staged.DefenderPartyId);
        Assert.Contains(mapEvent, Campaign.Current.MapEventManager.MapEvents);
        Assert.All(new[] { attacker, defender }, party => Assert.Same(mapEvent, party.MapEvent));
        Assert.Equal(2, mapEvent.TroopUpgradeTracker._mapEventParties.Count);
    });

    private static void AssertPending(EnvironmentInstance instance, MapEventContext staged, bool expected) =>
        instance.Call(() =>
        {
            var barrier = instance.Resolve<IMapEventInitializationBarrier>();
            var attacker = Get<MobileParty>(instance, staged.AttackerPartyId).Party;
            var defender = Get<MobileParty>(instance, staged.DefenderPartyId).Party;
            foreach (var party in new[] { attacker, defender })
            {
                Assert.Equal(expected, barrier.IsPartyPending(party));
                Assert.Equal(expected, party.MapEventSide == null);
            }
            Assert.Equal(!expected, PendingMapEventPartyMovementPatch.CanAdvancePosition(defender));
        });

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private IEnumerable<EnvironmentInstance> AllInstances => Clients.Prepend(Server);
}
