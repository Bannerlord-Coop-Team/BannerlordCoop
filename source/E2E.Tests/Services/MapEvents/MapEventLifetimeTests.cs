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
        var mapEventCtx = CreateServerMapEvent();

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        }
    }

    [Fact]
    public void Server_MapEvent_WasEverInLootingPhase_SyncAllClients()
    {
        var mapEventCtx = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out var mapEvent));
            mapEvent.WasEverInLootingPhase = true;
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out var mapEvent));
            Assert.True(mapEvent.WasEverInLootingPhase);
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
        var mapEventCtx = CreateServerMapEvent();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        }

        // Act
        DestroyServerMapEvent(mapEventCtx.MapEventId);

        // Assert
        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        }
    }

    [Fact]
    public void ClientFinalize_MapEvent_ServerAuthoritativelyDestroys()
    {
        // Arrange
        var mapEventCtx = CreateServerMapEvent();
        var firstClient = Clients.First();

        // Act — a client cannot finalize locally: FinalizeEvent is intercepted and forwarded to the server
        // as a request, which finalizes the battle authoritatively and replicates the removal to every peer.
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out var mapEvent));
            mapEvent.FinalizeEvent();
        }, MapEventDisabledMethods);

        // Assert — the server honored the request and the destroy replicated everywhere
        Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        }
    }
}
