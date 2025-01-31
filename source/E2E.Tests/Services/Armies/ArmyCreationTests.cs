using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class KingdomCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public KingdomCreationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateKingdom_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? kingdomId = null;
        server.Call(() =>
        {
            var kingdom = new Kingdom();

            Assert.True(server.ObjectManager.TryGetId(kingdom, out kingdomId));
        });

        // Assert
        Assert.NotNull(kingdomId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var _));
        }
    }

    [Fact]
    public void ClientCreateKingdom_DoesNothing()
    {
        // Arrange
        var client1 = TestEnvironment.Clients.First();

        // Act
        string? KingdomId = null;
        client1.Call(() =>
        {
            var Kingdom = new Kingdom();

            Assert.False(client1.ObjectManager.TryGetId(Kingdom, out KingdomId));
        });

        // Assert
        Assert.Null(KingdomId);
    }
}