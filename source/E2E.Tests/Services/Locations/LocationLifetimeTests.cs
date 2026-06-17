using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

/// <summary>
/// Verifies that <see cref="Location"/> creation is server authoritative and synced to every client
/// </summary>
public class LocationLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public LocationLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateLocation_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? locationId = null;
        server.Call(() =>
        {
            var location = GameObjectCreator.CreateInitializedObject<Location>();

            Assert.True(server.ObjectManager.TryGetId(location, out locationId));
        });

        // Assert
        Assert.NotNull(locationId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Location>(locationId, out var _));
        }
    }

    [Fact]
    public void ClientCreateLocation_DoesNothing()
    {
        // Arrange
        var client1 = TestEnvironment.Clients.First();

        // Act
        string? locationId = null;
        client1.Call(() =>
        {
            var location = new Location(
                stringId: "client_location",
                name: new TextObject("TestLocation"),
                doorName: new TextObject("TestLocationDoor"),
                prosperityMax: 100,
                isIndoor: true,
                canBeReserved: false,
                playerCanEnter: "CanAlways",
                playerCanSee: "CanAlways",
                aiCanExit: "CanAlways",
                aiCanEnter: "CanAlways",
                sceneNames: new string[4],
                locationComplex: new LocationComplex());

            Assert.False(client1.ObjectManager.TryGetId(location, out locationId));
        });

        // Assert
        Assert.Null(locationId);
    }
}
