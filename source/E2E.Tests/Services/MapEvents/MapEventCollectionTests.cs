using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventCollectionTests : MapEventTestBase
{
    public MapEventCollectionTests(ITestOutputHelper output) : base(output) { }

    [Fact(Skip = "MapEvent._sides is a fixed-size MapEventSide[2] array, not a dynamic collection; AssertCollectionReferenceField does not apply")]
    public void Server_MapEvent_Sides_IsFixedArray() { }

    [Fact]
    public void Server_MapEvent_Initialize_SyncsSidesToClients()
    {
        // Act
        var mapEventCtx = CreateServerMapEvent();

        // Resolve the side IDs from the server
        string? attackerSideId = null;
        string? defenderSideId = null;
        string? attackerMapEventPartyId = null;
        string? defenderMapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.AttackerSide, out attackerSideId));
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.DefenderSide, out defenderSideId));
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.AttackerSide.Parties[0], out attackerMapEventPartyId));
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.DefenderSide.Parties[0], out defenderMapEventPartyId));
        });

        Assert.NotNull(attackerSideId);
        Assert.NotNull(defenderSideId);
        Assert.NotNull(attackerMapEventPartyId);
        Assert.NotNull(defenderMapEventPartyId);

        // Assert — sides and parties propagated to all clients
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(attackerSideId, out _));
            Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(defenderSideId, out _));
            Assert.True(client.ObjectManager.TryGetObject<MapEventParty>(attackerMapEventPartyId, out _));
            Assert.True(client.ObjectManager.TryGetObject<MapEventParty>(defenderMapEventPartyId, out _));
        }
    }
}
