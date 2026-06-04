using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventLifetimeTests : MapEventTestBase
{
    public MapEventLifetimeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ServerCreate_MapEvent_SyncAllClients()
    {
        // Act
        var mapEventId = CreateServerMapEvent();

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));
        }
    }

    [Fact]
    public void ClientCreate_MapEvent_DoesNothing()
    {
        // Arrange
        var firstClient = Clients.First();
        string? mapEventId = null;

        // Act — clients must not be able to authoritatively create MapEvents
        firstClient.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            Assert.False(firstClient.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        // Assert
        Assert.Null(mapEventId);
        Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId ?? string.Empty, out _));
    }

    [Fact]
    public void ServerDestroy_MapEvent_SyncAllClients()
    {
        // Arrange
        var mapEventId = CreateServerMapEvent();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));
        }

        // Act
        DestroyServerMapEvent(mapEventId);

        // Assert
        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));
        }
    }

    [Fact]
    public void ClientDestroy_MapEvent_DoesNothing()
    {
        // Arrange
        var mapEventId = CreateServerMapEvent();
        var firstClient = Clients.First();

        // Act — a client calling FinalizeEvent must not remove the MapEvent from any peer
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            mapEvent.FinalizeEvent();
        }, MapEventDisabledMethods);

        // Assert — the event must still be registered everywhere
        Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));
        }
    }
}
