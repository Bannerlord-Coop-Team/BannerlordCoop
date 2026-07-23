using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Towns;

public class TownLifetimeTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public TownLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateTown_SyncAllClients()
    {
        // Arrange
        string? townId = null;

        // Act
        Server.Call(() =>
        {
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            Assert.True(Server.ObjectManager.TryGetId(town, out townId));
        });

        // Assert
        Assert.NotNull(townId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var _));
        }
    }

    [Fact]
    public void ClientCreateTown_DoesNothing()
    {
        // Arrange
        string? clientTownId = null;

        // Act
        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            var town = ObjectHelper.SkipConstructor<Town>();

            Assert.False(firstClient.ObjectManager.TryGetId(town, out clientTownId));
        });

        // Assert
        Assert.Null(clientTownId);
    }
}

